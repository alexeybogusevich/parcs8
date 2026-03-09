using System.Text;

namespace Parcs.Modules.TravelingSalesman.Models
{
    /// <summary>
    /// Generates SVG visualisations for TSP results.
    /// All output is self-contained single-file SVG (no external dependencies).
    /// </summary>
    public static class SvgGenerator
    {
        // ── Catppuccin Mocha colour palette ─────────────────────────────────────
        private const string BgColour    = "#1e1e2e"; // base
        private const string GridColour  = "#313244"; // surface0
        private const string AxisColour  = "#6c7086"; // overlay0
        private const string EdgeColour  = "#89b4fa"; // blue
        private const string NodeColour  = "#cba6f7"; // mauve
        private const string StartColour = "#f38ba8"; // red
        private const string LineColour  = "#a6e3a1"; // green
        private const string LabelColour = "#a6adc8"; // subtext0
        private const string TitleColour = "#89dceb"; // sky
        private const string TextColour  = "#cdd6f4"; // text

        // ── Route visualisation ──────────────────────────────────────────────────

        /// <summary>
        /// Generates an SVG image of the TSP tour.
        /// Cities are drawn as dots; the best route is drawn as connected edges.
        /// The start city is highlighted in red.
        /// City IDs are labelled when the problem has ≤ 60 cities.
        /// </summary>
        public static string GenerateRouteSvg(
            List<City> cities,
            List<int>  bestRoute,
            int        width  = 700,
            int        height = 700)
        {
            if (cities.Count == 0 || bestRoute.Count == 0)
                return EmptyChart(width, height, "No route data");

            double minX = cities.Min(c => c.X);
            double maxX = cities.Max(c => c.X);
            double minY = cities.Min(c => c.Y);
            double maxY = cities.Max(c => c.Y);

            double rangeX = Math.Max(maxX - minX, 1);
            double rangeY = Math.Max(maxY - minY, 1);

            const int pad = 50;
            double scale = Math.Min(
                (width  - 2 * pad) / rangeX,
                (height - 2 * pad) / rangeY);

            // SVG Y axis points down; world-Y high → SVG-Y low
            (double sx, double sy) Tx(City c) => (
                pad + (c.X - minX) * scale,
                height - pad - (c.Y - minY) * scale
            );

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
            sb.AppendLine($"  <rect width=\"{width}\" height=\"{height}\" fill=\"{BgColour}\"/>");

            // --- Route edges ---
            sb.AppendLine($"  <g stroke=\"{EdgeColour}\" stroke-width=\"1.5\" stroke-linejoin=\"round\" opacity=\"0.75\">");
            for (int i = 0; i < bestRoute.Count; i++)
            {
                var from = cities[bestRoute[i]];
                var to   = cities[bestRoute[(i + 1) % bestRoute.Count]];
                var (x1, y1) = Tx(from);
                var (x2, y2) = Tx(to);
                sb.AppendLine($"    <line x1=\"{x1:F1}\" y1=\"{y1:F1}\" x2=\"{x2:F1}\" y2=\"{y2:F1}\"/>");
            }
            sb.AppendLine("  </g>");

            // --- City dots (and optional labels) ---
            bool labelCities = cities.Count <= 60;
            sb.AppendLine($"  <g fill=\"{NodeColour}\">");
            foreach (var city in cities)
            {
                var (cx, cy) = Tx(city);
                sb.AppendLine($"    <circle cx=\"{cx:F1}\" cy=\"{cy:F1}\" r=\"4\"/>");
                if (labelCities)
                    sb.AppendLine($"    <text x=\"{cx + 6:F1}\" y=\"{cy + 4:F1}\" fill=\"{LabelColour}\" font-size=\"9\" font-family=\"monospace\">{city.Id}</text>");
            }
            sb.AppendLine("  </g>");

            // --- Start city highlighted ---
            if (bestRoute.Count > 0)
            {
                var start     = cities[bestRoute[0]];
                var (sx2, sy2) = Tx(start);
                sb.AppendLine($"  <circle cx=\"{sx2:F1}\" cy=\"{sy2:F1}\" r=\"7\" fill=\"{StartColour}\"/>");
            }

            // --- Title ---
            sb.AppendLine($"  <text x=\"{width / 2}\" y=\"22\" fill=\"{TextColour}\" font-size=\"13\" font-family=\"sans-serif\" text-anchor=\"middle\">{cities.Count} cities — TSP best route</text>");

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        // ── Convergence chart ────────────────────────────────────────────────────

        /// <summary>
        /// Generates an SVG line chart of best-distance convergence over generations.
        /// Each history entry represents one checkpoint (recorded every 5 generations).
        /// </summary>
        public static string GenerateConvergenceSvg(
            List<double> history,
            int          width  = 700,
            int          height = 260)
        {
            if (history.Count < 2)
                return EmptyChart(width, height, "Insufficient convergence data");

            double minVal = history.Min();
            double maxVal = history.Max();
            double range  = Math.Max(maxVal - minVal, 1);

            const int padL = 75;
            const int padR = 25;
            const int padT = 35;
            const int padB = 40;
            double chartW = width  - padL - padR;
            double chartH = height - padT - padB;

            double scaleX = chartW / Math.Max(history.Count - 1, 1);
            double scaleY = chartH / range;

            (double px, double py) Tx(int i, double v) => (
                padL + i * scaleX,
                padT + chartH - (v - minVal) * scaleY
            );

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
            sb.AppendLine($"  <rect width=\"{width}\" height=\"{height}\" fill=\"{BgColour}\"/>");

            // --- Horizontal grid lines ---
            sb.AppendLine($"  <g stroke=\"{GridColour}\" stroke-width=\"1\">");
            for (int i = 1; i <= 4; i++)
            {
                double y = padT + chartH * i / 4.0;
                sb.AppendLine($"    <line x1=\"{padL}\" y1=\"{y:F0}\" x2=\"{padL + chartW:F0}\" y2=\"{y:F0}\"/>");
            }
            sb.AppendLine("  </g>");

            // --- Axes ---
            sb.AppendLine($"  <line x1=\"{padL}\" y1=\"{padT}\" x2=\"{padL}\" y2=\"{padT + chartH}\" stroke=\"{AxisColour}\" stroke-width=\"1\"/>");
            sb.AppendLine($"  <line x1=\"{padL}\" y1=\"{padT + chartH}\" x2=\"{padL + chartW:F0}\" y2=\"{padT + chartH}\" stroke=\"{AxisColour}\" stroke-width=\"1\"/>");

            // --- Convergence polyline ---
            var pts = new StringBuilder();
            for (int i = 0; i < history.Count; i++)
            {
                var (px, py) = Tx(i, history[i]);
                if (i > 0) pts.Append(' ');
                pts.Append($"{px:F1},{py:F1}");
            }
            sb.AppendLine($"  <polyline points=\"{pts}\" fill=\"none\" stroke=\"{LineColour}\" stroke-width=\"2\" stroke-linejoin=\"round\"/>");

            // --- Y-axis labels (top, mid, bottom) ---
            double midVal = minVal + range / 2;
            var (_, yTop) = Tx(0, maxVal);
            var (_, yMid) = Tx(0, midVal);
            var (_, yBot) = Tx(0, minVal);
            sb.AppendLine($"  <text x=\"{padL - 6}\" y=\"{yTop + 4:F0}\" fill=\"{LabelColour}\" font-size=\"10\" font-family=\"monospace\" text-anchor=\"end\">{maxVal:F0}</text>");
            sb.AppendLine($"  <text x=\"{padL - 6}\" y=\"{yMid + 4:F0}\" fill=\"{LabelColour}\" font-size=\"10\" font-family=\"monospace\" text-anchor=\"end\">{midVal:F0}</text>");
            sb.AppendLine($"  <text x=\"{padL - 6}\" y=\"{yBot + 4:F0}\" fill=\"{LabelColour}\" font-size=\"10\" font-family=\"monospace\" text-anchor=\"end\">{minVal:F0}</text>");

            // --- X-axis labels ---
            // Each history entry = 5 generations
            int lastGen = (history.Count - 1) * 5;
            sb.AppendLine($"  <text x=\"{padL}\" y=\"{padT + chartH + 14}\" fill=\"{LabelColour}\" font-size=\"10\" font-family=\"monospace\" text-anchor=\"middle\">0</text>");
            sb.AppendLine($"  <text x=\"{padL + chartW:F0}\" y=\"{padT + chartH + 14}\" fill=\"{LabelColour}\" font-size=\"10\" font-family=\"monospace\" text-anchor=\"middle\">{lastGen}</text>");
            sb.AppendLine($"  <text x=\"{padL + chartW / 2:F0}\" y=\"{padT + chartH + 28}\" fill=\"{AxisColour}\" font-size=\"10\" font-family=\"sans-serif\" text-anchor=\"middle\">generation</text>");

            // --- Title and improvement annotation ---
            sb.AppendLine($"  <text x=\"{padL + chartW / 2:F0}\" y=\"20\" fill=\"{TitleColour}\" font-size=\"12\" font-family=\"sans-serif\" text-anchor=\"middle\">Convergence — best distance over generations</text>");

            double improvement    = history[0] - history[^1];
            double improvementPct = history[0] > 0 ? improvement / history[0] * 100 : 0;
            sb.AppendLine($"  <text x=\"{padL + chartW:F0}\" y=\"{padT - 6}\" fill=\"{LineColour}\" font-size=\"10\" font-family=\"monospace\" text-anchor=\"end\">improved {improvementPct:F1}%</text>");

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private static string EmptyChart(int width, int height, string message) =>
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\">" +
            $"<rect width=\"{width}\" height=\"{height}\" fill=\"{BgColour}\"/>" +
            $"<text x=\"{width / 2}\" y=\"{height / 2}\" fill=\"{LabelColour}\" font-size=\"12\" font-family=\"sans-serif\" text-anchor=\"middle\">{message}</text>" +
            "</svg>";
    }
}
