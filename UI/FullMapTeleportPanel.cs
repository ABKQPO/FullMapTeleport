using System;
using TerrariaModder.Core.UI.Widgets;

namespace FullMapTeleport.UI
{
    public sealed class FullMapTeleportPanel
    {
        private const string PanelId = "full-map-teleport-panel";
        private readonly Action _revealFullMap;
        private readonly DraggablePanel _panel;

        public FullMapTeleportPanel(Action revealFullMap)
        {
            _revealFullMap = revealFullMap;
            _panel = new DraggablePanel(PanelId, "Full Map Teleport", 360, 170)
            {
                CloseOnEscape = true,
                ClipContent = false
            };
        }

        public void RegisterDrawCallback() => _panel.RegisterDrawCallback(Draw);
        public void UnregisterDrawCallback() => _panel.UnregisterDrawCallback();
        public void Toggle() => _panel.Toggle();
        public void Close() => _panel.Close();

        private void Draw()
        {
            if (!_panel.BeginDraw())
                return;

            try
            {
                var layout = new StackLayout(_panel.ContentX, _panel.ContentY, _panel.ContentWidth, spacing: 8);
                layout.Label("Map utilities", 20);
                if (layout.Button("Reveal Full Map (Max Brightness)", 32))
                    _revealFullMap?.Invoke();
                layout.Label("Reveals all explored map tiles at full brightness.", 20);
            }
            finally
            {
                _panel.EndDraw();
            }
        }
    }
}
