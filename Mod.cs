using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.Map;
using TerrariaModder.Core;
using TerrariaModder.Core.Config;
using TerrariaModder.Core.Events;
using TerrariaModder.Core.Logging;
using FullMapTeleport.UI;

namespace FullMapTeleport
{
    public sealed class FullMapTeleportConfig : ModConfig
    {
        public override int Version => 1;

        [Client, Label("Enabled"), Description("Enable right-click map teleportation and unrestricted map icon teleportation.")]
        public bool Enabled { get; set; } = true;

        [Client, Label("Reveal Full Map"), Description("Click once in F6 to reveal the entire map at maximum brightness.")]
        public bool RevealFullMap { get; set; }
    }

    public sealed class Mod : IMod
    {
        private const string HarmonyId = "com.terrariamodder.fullmapteleport";

        private ILogger _log;
        private Harmony _harmony;
        private bool _patchesApplied;
        private FullMapTeleportPanel _panel;
        private bool _revealRequestHandled;

        [ThreadStatic]
        private static bool _mapDrawActive;

        private static readonly ButtonPressLatch RightClickLatch = new ButtonPressLatch();
        private static bool _pendingTeleport;
        private static TilePoint _pendingTarget;

        public string Id => "full-map-teleport";
        public string Name => "Full Map Teleport";
        public string Version => "1.1.1";

        private static FullMapTeleportConfig Current { get; set; }
        private static ILogger InstanceLog { get; set; }

        public void Initialize(ModContext context)
        {
            _log = context.Logger;
            InstanceLog = _log;
            Current = context.GetConfig<FullMapTeleportConfig>();
            _harmony = new Harmony(HarmonyId);

            ApplyPatches(context.IsServer);
            if (!context.IsServer)
            {
                FrameEvents.OnPostUpdate += OnPostUpdate;
                _panel = new FullMapTeleportPanel(RevealFullMap);
                _panel.RegisterDrawCallback();
                context.RegisterKeybind("toggle-panel", "Toggle Full Map Teleport Panel", "Open the map utility panel", "F7", TogglePanel);
            }
            _log.Info($"{Name} v{Version} initialized");
        }

        public void Unload()
        {
            _mapDrawActive = false;
            RightClickLatch.Reset();
            _pendingTeleport = false;
            FrameEvents.OnPostUpdate -= OnPostUpdate;
            _panel?.Close();
            _panel?.UnregisterDrawCallback();
            _panel = null;
            Current = null;
            InstanceLog = null;
            try
            {
                _harmony?.UnpatchAll(HarmonyId);
            }
            catch (Exception ex)
            {
                _log?.Warn($"Failed to remove Harmony patches: {ex.Message}");
            }

            _harmony = null;
            _patchesApplied = false;
        }

        public void OnConfigChanged()
        {
            if (_configRevealAlreadyHandled())
                return;

            if (Current != null && Current.RevealFullMap)
            {
                _revealRequestHandled = true;
                RevealFullMap();
                Current.RevealFullMap = false;
                try { Current.Save(); } catch (Exception ex) { _log?.Warn($"Failed to save reveal action reset: {ex.Message}"); }
            }
            else
            {
                _revealRequestHandled = false;
            }
        }

        private bool _configRevealAlreadyHandled()
        {
            return _revealRequestHandled && (Current == null || !Current.RevealFullMap);
        }

        private void TogglePanel()
        {
            if (Main.gameMenu || Main.netMode == 2)
                return;
            _panel?.Toggle();
        }

        private void RevealFullMap()
        {
            if (Main.gameMenu || Main.netMode == 2 || Main.Map == null)
                return;

            try
            {
                MapRevealBounds bounds = MapRevealMath.GetBounds(Main.maxTilesX, Main.maxTilesY, WorldMap.BlackEdgeWidth);
                for (int x = bounds.Left; x < bounds.RightExclusive; x++)
                {
                    for (int y = bounds.Top; y < bounds.BottomExclusive; y++)
                        Main.Map.Update(x, y, byte.MaxValue);
                }

                Main.refreshMap = true;
                Main.Map.Save();
                _log?.Info("Full map revealed at maximum brightness.");
            }
            catch (Exception ex)
            {
                _log?.Warn($"Full map reveal failed: {ex.Message}");
            }
        }

        private static bool IsEnabled()
        {
            return Current == null || Current.Enabled;
        }

        private void ApplyPatches(bool dedicatedServer)
        {
            if (_patchesApplied || _harmony == null)
                return;

            try
            {
                PatchPylonRequest(dedicatedServer);
                PatchPylonProximity(dedicatedServer);
                PatchInstantReturnItems(dedicatedServer);

                if (!dedicatedServer)
                {
                    MethodInfo drawMap = AccessTools.Method(
                        typeof(Main), "DrawMap", new[] { typeof(GameTime) });
                    MethodInfo hasUnityPotion = AccessTools.Method(
                        typeof(Player), "HasUnityPotion", Type.EmptyTypes);

                    if (drawMap == null)
                        _log.Warn("Main.DrawMap(GameTime) was not found; right-click teleport disabled");
                    else
                    {
                        _harmony.Patch(drawMap,
                            prefix: new HarmonyMethod(typeof(Mod), nameof(DrawMapPrefix)),
                            postfix: new HarmonyMethod(typeof(Mod), nameof(DrawMapPostfix)));
                    }

                    if (hasUnityPotion == null)
                        _log.Warn("Player.HasUnityPotion() was not found; teammate restriction remains vanilla");
                    else
                        _harmony.Patch(hasUnityPotion,
                            postfix: new HarmonyMethod(typeof(Mod), nameof(HasUnityPotionPostfix)));
                }

                _patchesApplied = true;
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to apply map teleport patches: {ex.Message}");
            }
        }

        private void PatchInstantReturnItems(bool dedicatedServer)
        {
            MethodInfo itemUseMethod;
            string postfixName;

            if (dedicatedServer)
            {
                Type serverPlayer = FindTerrariaServerAssembly()?.GetType("Terraria.Player");
                itemUseMethod = serverPlayer?.GetMethod(
                    "ItemCheck_StartActualUse",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { serverPlayer.Assembly.GetType("Terraria.Item") },
                    null);
                postfixName = nameof(InstantReturnItemServerPostfix);
            }
            else
            {
                itemUseMethod = AccessTools.Method(
                    typeof(Player), "ItemCheck_StartActualUse", new[] { typeof(Item) });
                postfixName = nameof(InstantReturnItemClientPostfix);
            }

            if (itemUseMethod == null)
            {
                _log.Warn("Player.ItemCheck_StartActualUse was not found; return item timing remains vanilla.");
                return;
            }

            _harmony.Patch(itemUseMethod,
                postfix: new HarmonyMethod(typeof(Mod), postfixName));
            _log.Info($"Patched instant return item timing ({itemUseMethod.DeclaringType.Assembly.GetName().Name}).");
        }

        private static void InstantReturnItemClientPostfix(Player __instance, Item __0)
        {
            if (!IsEnabled() || __instance == null || __0 == null ||
                !TeleportMath.IsInstantReturnItem(__0.type))
                return;

            __instance.itemTime = TeleportMath.GetInstantReturnTriggerTime(__0.useTime);
        }

        private static void InstantReturnItemServerPostfix(object __instance, object __0)
        {
            if (!IsEnabled() || __instance == null || __0 == null)
                return;

            try
            {
                int itemType = ReadIntMember(__0, "type");
                if (!TeleportMath.IsInstantReturnItem(itemType))
                    return;

                int useTime = ReadIntMember(__0, "useTime");
                SetIntMember(__instance, "itemTime", TeleportMath.GetInstantReturnTriggerTime(useTime));
            }
            catch (Exception ex)
            {
                InstanceLog?.Warn($"Instant return item timing patch failed: {ex.Message}");
            }
        }

        private void PatchPylonRequest(bool dedicatedServer)
        {
            MethodInfo requestMethod;
            string prefixName;

            if (dedicatedServer)
            {
                Assembly serverAssembly = FindTerrariaServerAssembly();
                Type serverPylonSystem = serverAssembly?.GetType("Terraria.GameContent.TeleportPylonsSystem");
                requestMethod = serverPylonSystem?.GetMethod(
                    "HandleTeleportRequest",
                    BindingFlags.Public | BindingFlags.Instance);
                prefixName = nameof(HandleTeleportRequestServerPrefix);
            }
            else
            {
                requestMethod = AccessTools.Method(
                    typeof(TeleportPylonsSystem),
                    "HandleTeleportRequest",
                    new[] { typeof(TeleportPylonInfo), typeof(int) });
                prefixName = nameof(HandleTeleportRequestClientPrefix);
            }

            if (requestMethod == null)
            {
                _log.Warn("TeleportPylonsSystem.HandleTeleportRequest was not found; pylon restrictions remain vanilla.");
                return;
            }

            _harmony.Patch(requestMethod,
                prefix: new HarmonyMethod(typeof(Mod), prefixName));
            _log.Info($"Patched pylon request handler ({requestMethod.DeclaringType.Assembly.GetName().Name}).");
        }

        private void PatchPylonProximity(bool dedicatedServer)
        {
            Type pylonSystemType;
            if (dedicatedServer)
            {
                pylonSystemType = FindTerrariaServerAssembly()?.GetType(
                    "Terraria.GameContent.TeleportPylonsSystem");
            }
            else
            {
                pylonSystemType = typeof(TeleportPylonsSystem);
            }

            MethodInfo proximityMethod = pylonSystemType?.GetMethod(
                "IsPlayerNearAPylon",
                BindingFlags.Public | BindingFlags.Static);
            if (proximityMethod == null)
            {
                _log.Warn("TeleportPylonsSystem.IsPlayerNearAPylon was not found; pylon icons may remain gray.");
                return;
            }

            _harmony.Patch(proximityMethod,
                postfix: new HarmonyMethod(typeof(Mod), nameof(PylonProximityPostfix)));
            _log.Info($"Patched pylon proximity check ({proximityMethod.DeclaringType.Assembly.GetName().Name}).");
        }

        private static void DrawMapPrefix()
        {
            _mapDrawActive = true;
        }

        private static void DrawMapPostfix()
        {
            _mapDrawActive = false;
            TryTeleportFromRightClick();
        }

        private static void HasUnityPotionPostfix(ref bool __result)
        {
            if (_mapDrawActive && Main.mapFullscreen && IsEnabled())
                __result = true;
        }

        private static bool HandleTeleportRequestClientPrefix(TeleportPylonInfo info, int playerIndex)
        {
            if (!IsEnabled())
                return true;

            try
            {
                if (Main.player == null || playerIndex < 0 || playerIndex >= Main.player.Length)
                    return true;

                Player player = Main.player[playerIndex];
                if (player == null || !player.active)
                    return true;

                Vector2 newPosition = info.PositionInTiles.ToWorldCoordinates()
                    - new Vector2(0f, player.HeightOffsetBoost);
                const int teleportStyle = 9;
                int pylonStyle = (int)info.TypeOfPylon;

                player.Teleport(newPosition, teleportStyle, pylonStyle);
                player.velocity = Vector2.Zero;

                if (Main.netMode == 2)
                {
                    RemoteClient.CheckSection(player.whoAmI, player.position);
                    NetMessage.SendData(65, -1, -1, null, 0, player.whoAmI,
                        newPosition.X, newPosition.Y, teleportStyle, 0, pylonStyle);
                }

                return false;
            }
            catch (Exception ex)
            {
                InstanceLog?.Warn($"Unrestricted pylon teleport failed: {ex.Message}");
                return true;
            }
        }

        private static void PylonProximityPostfix(ref bool __result)
        {
            if (IsEnabled())
                __result = true;
        }

        private static bool HandleTeleportRequestServerPrefix(object __instance, object __0, int __1)
        {
            if (!IsEnabled())
                return true;

            try
            {
                Assembly serverAssembly = __instance?.GetType().Assembly;
                Type mainType = serverAssembly?.GetType("Terraria.Main");
                Array players = mainType?.GetField("player", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as Array;
                int playerIndex = __1;
                object info = __0;
                if (players == null || playerIndex < 0 || playerIndex >= players.Length)
                    return true;

                object player = players.GetValue(playerIndex);
                if (player == null || !ReadBoolMember(player, "active"))
                    return true;

                object positionInTiles = ReadMember(info, "PositionInTiles");
                int tileX = ReadIntMember(positionInTiles, "X");
                int tileY = ReadIntMember(positionInTiles, "Y");
                int pylonStyle = Convert.ToInt32(ReadMember(info, "TypeOfPylon"));
                float heightOffset = ReadFloatMember(player, "HeightOffsetBoost");
                Vector2 position = new Vector2(tileX * 16f, TeleportMath.PylonWorldY(tileY, heightOffset));

                MethodInfo teleport = player.GetType().GetMethod(
                    "Teleport",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(Vector2), typeof(int), typeof(int) },
                    null);
                FieldInfo velocity = player.GetType().GetField("velocity", BindingFlags.Public | BindingFlags.Instance);
                if (teleport == null || velocity == null)
                    return true;

                teleport.Invoke(player, new object[] { position, 9, pylonStyle });
                velocity.SetValue(player, Vector2.Zero);
                SyncServerTeleport(serverAssembly, playerIndex, position, pylonStyle);
                return false;
            }
            catch (Exception ex)
            {
                InstanceLog?.Warn($"Unrestricted dedicated-server pylon teleport failed: {ex.Message}");
                return true;
            }
        }

        private static Assembly FindTerrariaServerAssembly()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "TerrariaServer")
                    return assembly;
            }

            return null;
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null)
                return null;

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
                return property.GetValue(instance, null);

            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(instance);
        }

        private static int ReadIntMember(object instance, string name)
        {
            object value = ReadMember(instance, name);
            if (value == null)
                throw new InvalidOperationException($"Missing integer member: {name}");
            return Convert.ToInt32(value);
        }

        private static float ReadFloatMember(object instance, string name)
        {
            object value = ReadMember(instance, name);
            if (value == null)
                throw new InvalidOperationException($"Missing floating-point member: {name}");
            return Convert.ToSingle(value);
        }

        private static bool ReadBoolMember(object instance, string name)
        {
            object value = ReadMember(instance, name);
            return value != null && Convert.ToBoolean(value);
        }

        private static void SetIntMember(object instance, string name, int value)
        {
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value, null);
                return;
            }

            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException($"Missing writable integer member: {name}");
            field.SetValue(instance, value);
        }

        private static void SyncServerTeleport(Assembly serverAssembly, int playerIndex, Vector2 position, int pylonStyle)
        {
            Type remoteClient = serverAssembly.GetType("Terraria.RemoteClient");
            MethodInfo checkSection = remoteClient?.GetMethod(
                "CheckSection",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(Vector2) },
                null);
            checkSection?.Invoke(null, new object[] { playerIndex, position });

            Type netMessage = serverAssembly.GetType("Terraria.NetMessage");
            MethodInfo sendData = null;
            if (netMessage != null)
            {
                foreach (MethodInfo method in netMessage.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name == "SendData" && method.GetParameters().Length == 11)
                    {
                        sendData = method;
                        break;
                    }
                }
            }

            sendData?.Invoke(null, new object[]
            {
                65, -1, -1, null, 0, playerIndex, position.X, position.Y, 9f, 0, pylonStyle
            });
        }

        private static void TryTeleportFromRightClick()
        {
            if (!Main.mapFullscreen)
            {
                RightClickLatch.Reset();
                return;
            }

            bool isNewRightClick = RightClickLatch.ConsumeNewPress(Main.mouseRight);
            if (!IsEnabled() || !isNewRightClick || Main.gameMenu || Main.netMode == 2)
                return;
            if (Main.LocalPlayer == null || Main.LocalPlayer.dead)
                return;

            float scale = Main.mapFullscreenScale;
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
                return;

            TilePoint target = TeleportMath.MapScreenToTile(
                Main.mouseX,
                Main.mouseY,
                Main.screenWidth,
                Main.screenHeight,
                Main.mapFullscreenPos.X,
                Main.mapFullscreenPos.Y,
                scale);
            target = TeleportMath.Clamp(target, 10, Main.maxTilesX - 10, 10, Main.maxTilesY - 19);

                _pendingTarget = target;
                _pendingTeleport = true;
                Main.mouseRightRelease = false;
        }

        private static void ExecutePendingTeleport()
        {
            if (!_pendingTeleport)
                return;

            _pendingTeleport = false;
            if (!IsEnabled() || Main.gameMenu || Main.netMode == 2 ||
                Main.LocalPlayer == null || Main.LocalPlayer.dead)
                return;

            try
            {
                Player player = Main.LocalPlayer;
                WorldPoint playerPosition = TeleportMath.MapTileToPlayerTopLeft(
                    _pendingTarget, player.width, player.height);
                Vector2 targetPosition = new Vector2(playerPosition.X, playerPosition.Y);
                player.Teleport(targetPosition, 1, 0);
                player.velocity = Vector2.Zero;

                if (Main.netMode == 1)
                    NetMessage.SendData(13, -1, -1, null, player.whoAmI);

                InstanceLog?.Info($"Teleported to map tile ({_pendingTarget.X}, {_pendingTarget.Y})");
            }
            catch (Exception ex)
            {
                InstanceLog?.Warn($"Pending map teleport failed: {ex.Message}");
            }
        }

        private static void OnPostUpdate()
        {
            ExecutePendingTeleport();
        }
    }
}
