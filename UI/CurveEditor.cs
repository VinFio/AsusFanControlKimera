using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using AsusFanControlKimera.Localization;

namespace AsusFanControlKimera.UI
{
    internal sealed class CurveEditor : Control
    {
        private const int LeftMargin = 48;
        private const int RightMargin = 20;
        private const int TopMargin = 20;
        private const int BottomMargin = 38;
        private static readonly Color GridColor = Color.FromArgb(225, 229, 235);
        private static readonly Color AxisColor = Color.FromArgb(70, 75, 85);
        private static readonly Color LineColor = Color.FromArgb(0, 120, 215);
        private List<Point> points = new List<Point>();
        private int draggedIndex = -1;
        // Posizione del punto trascinato in pixel: segue il mouse senza scatti,
        // mentre il valore salvato resta arrotondato a gradi e percentuali interi.
        private PointF? dragPosition;
        // Griglia, assi ed etichette non cambiano durante il trascinamento:
        // vengono disegnati una sola volta e riutilizzati a ogni ridisegno.
        private Bitmap background;

        internal event EventHandler CurveChanged;

        internal IList<Point> Points
        {
            get { return points.Select(p => new Point(p.X, p.Y)).ToList(); }
            set
            {
                points = value.OrderBy(p => p.X).Select(p => new Point(p.X, p.Y)).ToList();
                Invalidate();
            }
        }

        internal CurveEditor()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);
            BackColor = Color.White;
            MinimumSize = new Size(480, 260);
            Cursor = Cursors.Cross;
        }

        /// <summary>Ridisegna le etichette, per esempio dopo un cambio di lingua.</summary>
        internal void RefreshText()
        {
            DiscardBackground();
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            DiscardBackground();
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            DiscardBackground();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DiscardBackground();
            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Tutto lo sfondo è nella bitmap disegnata da OnPaint.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImageUnscaled(GetBackground(), 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var graphPoints = new PointF[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                graphPoints[i] = i == draggedIndex && dragPosition.HasValue
                    ? dragPosition.Value
                    : new PointF(GraphX(points[i].X), GraphY(points[i].Y));
            }

            if (graphPoints.Length > 1)
            {
                using (var linePen = new Pen(LineColor, 3) { LineJoin = LineJoin.Round })
                    g.DrawLines(linePen, graphPoints);
            }

            for (int i = 0; i < graphPoints.Length; i++)
            {
                PointF p = graphPoints[i];
                Brush brush = i == draggedIndex ? Brushes.OrangeRed : Brushes.SeaGreen;
                g.FillEllipse(brush, p.X - 6, p.Y - 6, 12, 12);
                g.DrawEllipse(Pens.White, p.X - 5, p.Y - 5, 10, 10);
            }

            if (draggedIndex >= 0 && draggedIndex < points.Count)
                DrawValueLabel(g, points[draggedIndex], graphPoints[draggedIndex]);
        }

        private void DrawValueLabel(Graphics g, Point value, PointF anchor)
        {
            string text = string.Format("{0} °C  ·  {1}%", value.X, value.Y);
            SizeF size = g.MeasureString(text, Font);
            var box = new RectangleF(anchor.X + 10, anchor.Y - size.Height - 12,
                size.Width + 8, size.Height + 4);
            // Mantiene l'etichetta dentro il controllo.
            if (box.Right > Width - 2)
                box.X = anchor.X - box.Width - 10;
            if (box.Top < 2)
                box.Y = anchor.Y + 12;
            using (var fill = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                g.FillRectangle(fill, box);
            using (var border = new Pen(AxisColor))
                g.DrawRectangle(border, box.X, box.Y, box.Width, box.Height);
            g.DrawString(text, Font, Brushes.Black, box.X + 4, box.Y + 2);
        }

        private Bitmap GetBackground()
        {
            if (background != null && background.Size == ClientSize)
                return background;

            DiscardBackground();
            background = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
            using (Graphics g = Graphics.FromImage(background))
            {
                g.Clear(BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Rectangle plot = PlotRectangle;
                using (var gridPen = new Pen(GridColor))
                using (var axisPen = new Pen(AxisColor, 1.5f))
                {
                    for (int temperature = 20; temperature <= 100; temperature += 10)
                    {
                        float x = GraphX(temperature);
                        g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                        g.DrawString(temperature.ToString(), Font, Brushes.DimGray, x - 9, plot.Bottom + 7);
                    }
                    for (int speed = 0; speed <= 100; speed += 20)
                    {
                        float y = GraphY(speed);
                        g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                        g.DrawString(speed.ToString(), Font, Brushes.DimGray, 9, y - 7);
                    }
                    g.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
                    g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
                }

                g.DrawString(Strings.Get("CurveTemperatureAxis"), Font, Brushes.DimGray,
                    plot.Left + plot.Width / 2 - 45, Height - 18);
                g.DrawString("PWM %", Font, Brushes.DimGray, 4, 2);
            }
            return background;
        }

        private void DiscardBackground()
        {
            if (background == null)
                return;
            background.Dispose();
            background = null;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left || !PlotRectangle.Contains(e.Location))
                return;
            Point value = ToCurvePoint(e.Location);
            if (points.Any(p => p.X == value.X))
                return;
            // Come nel trascinamento, la velocità non può scendere al salire della temperatura.
            int minimumY = points.Where(p => p.X < value.X).Select(p => p.Y).DefaultIfEmpty(1).Max();
            int maximumY = points.Where(p => p.X > value.X).Select(p => p.Y).DefaultIfEmpty(100).Min();
            value.Y = Math.Max(minimumY, Math.Min(maximumY, value.Y));
            points.Add(value);
            NormalizePoints();
            RaiseChanged();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int nearest = FindNearest(e.Location);
            if (e.Button == MouseButtons.Right && nearest >= 0 && points.Count > 2)
            {
                points.RemoveAt(nearest);
                NormalizePoints();
                RaiseChanged();
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                draggedIndex = nearest;
                dragPosition = null;
                if (draggedIndex >= 0)
                    Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (draggedIndex < 0 || e.Button != MouseButtons.Left)
                return;
            int minimumX = draggedIndex == 0 ? 20 : points[draggedIndex - 1].X + 1;
            int maximumX = draggedIndex == points.Count - 1 ? 105 : points[draggedIndex + 1].X - 1;
            int minimumY = draggedIndex == 0 ? 1 : points[draggedIndex - 1].Y;
            int maximumY = draggedIndex == points.Count - 1 ? 100 : points[draggedIndex + 1].Y;

            Point value = ToCurvePoint(e.Location);
            value.X = Math.Max(minimumX, Math.Min(maximumX, value.X));
            value.Y = Math.Max(minimumY, Math.Min(maximumY, value.Y));

            var position = new PointF(
                Math.Max(GraphX(minimumX), Math.Min(GraphX(maximumX), e.X)),
                Math.Max(GraphY(maximumY), Math.Min(GraphY(minimumY), e.Y)));
            if (points[draggedIndex] == value && dragPosition == position)
                return;
            points[draggedIndex] = value;
            dragPosition = position;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (draggedIndex >= 0)
            {
                draggedIndex = -1;
                dragPosition = null;
                RaiseChanged();
            }
        }

        private Rectangle PlotRectangle
        {
            get { return new Rectangle(LeftMargin, TopMargin, Math.Max(1, Width - LeftMargin - RightMargin), Math.Max(1, Height - TopMargin - BottomMargin)); }
        }

        private float GraphX(float temperature)
        {
            Rectangle plot = PlotRectangle;
            return plot.Left + (temperature - 20) * plot.Width / 85f;
        }

        private float GraphY(float speed)
        {
            Rectangle plot = PlotRectangle;
            return plot.Bottom - speed * plot.Height / 100f;
        }

        private Point ToCurvePoint(Point screen)
        {
            Rectangle plot = PlotRectangle;
            int x = Math.Max(plot.Left, Math.Min(plot.Right, screen.X));
            int y = Math.Max(plot.Top, Math.Min(plot.Bottom, screen.Y));
            int temperature = 20 + (int)Math.Round((x - plot.Left) * 85.0 / plot.Width);
            int speed = (int)Math.Round((plot.Bottom - y) * 100.0 / plot.Height);
            return new Point(Math.Max(20, Math.Min(105, temperature)), Math.Max(1, Math.Min(100, speed)));
        }

        private int FindNearest(Point screen)
        {
            int found = -1;
            double distance = 14;
            for (int i = 0; i < points.Count; i++)
            {
                double dx = GraphX(points[i].X) - screen.X;
                double dy = GraphY(points[i].Y) - screen.Y;
                double candidate = Math.Sqrt(dx * dx + dy * dy);
                if (candidate < distance)
                {
                    found = i;
                    distance = candidate;
                }
            }
            return found;
        }

        private void NormalizePoints()
        {
            points = points.OrderBy(p => p.X).ToList();
            Invalidate();
        }

        private void RaiseChanged()
        {
            NormalizePoints();
            if (CurveChanged != null)
                CurveChanged(this, EventArgs.Empty);
        }
    }
}
