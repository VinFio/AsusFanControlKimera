using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace AsusFanControlKimera.UI
{
    internal sealed class CurveEditor : Control
    {
        private const int LeftMargin = 48;
        private const int RightMargin = 20;
        private const int TopMargin = 20;
        private const int BottomMargin = 38;
        private List<Point> points = new List<Point>();
        private int draggedIndex = -1;

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

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Invalidate();
            Update();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle plot = PlotRectangle;

            using (var gridPen = new Pen(Color.FromArgb(225, 229, 235)))
            using (var axisPen = new Pen(Color.FromArgb(70, 75, 85), 1.5f))
            {
                for (int temperature = 20; temperature <= 100; temperature += 10)
                {
                    int x = GraphX(temperature);
                    g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                    g.DrawString(temperature.ToString(), Font, Brushes.DimGray, x - 9, plot.Bottom + 7);
                }
                for (int speed = 0; speed <= 100; speed += 20)
                {
                    int y = GraphY(speed);
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                    g.DrawString(speed.ToString(), Font, Brushes.DimGray, 9, y - 7);
                }
                g.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
                g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            }

            g.DrawString("Temperatura °C", Font, Brushes.DimGray, plot.Left + plot.Width / 2 - 45, Height - 18);
            g.DrawString("PWM %", Font, Brushes.DimGray, 4, 2);

            if (points.Count > 1)
            {
                Point[] graphPoints = points.Select(p => new Point(GraphX(p.X), GraphY(p.Y))).ToArray();
                using (var linePen = new Pen(Color.FromArgb(0, 120, 215), 3))
                {
                    linePen.LineJoin = LineJoin.Round;
                    g.DrawLines(linePen, graphPoints);
                }
            }

            for (int i = 0; i < points.Count; i++)
            {
                Point p = new Point(GraphX(points[i].X), GraphY(points[i].Y));
                Brush brush = i == draggedIndex ? Brushes.OrangeRed : Brushes.SeaGreen;
                g.FillEllipse(brush, p.X - 6, p.Y - 6, 12, 12);
                g.DrawEllipse(Pens.White, p.X - 5, p.Y - 5, 10, 10);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left || !PlotRectangle.Contains(e.Location))
                return;
            Point value = ToCurvePoint(e.Location);
            if (points.Any(p => p.X == value.X))
                return;
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
                draggedIndex = nearest;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (draggedIndex < 0 || e.Button != MouseButtons.Left)
                return;
            Point value = ToCurvePoint(e.Location);
            int minimumX = draggedIndex == 0 ? 20 : points[draggedIndex - 1].X + 1;
            int maximumX = draggedIndex == points.Count - 1 ? 105 : points[draggedIndex + 1].X - 1;
            int minimumY = draggedIndex == 0 ? 1 : points[draggedIndex - 1].Y;
            int maximumY = draggedIndex == points.Count - 1 ? 100 : points[draggedIndex + 1].Y;
            value.X = Math.Max(minimumX, Math.Min(maximumX, value.X));
            value.Y = Math.Max(minimumY, Math.Min(maximumY, value.Y));
            points[draggedIndex] = value;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (draggedIndex >= 0)
            {
                draggedIndex = -1;
                RaiseChanged();
            }
        }

        private Rectangle PlotRectangle
        {
            get { return new Rectangle(LeftMargin, TopMargin, Math.Max(1, Width - LeftMargin - RightMargin), Math.Max(1, Height - TopMargin - BottomMargin)); }
        }

        private int GraphX(int temperature)
        {
            return PlotRectangle.Left + (temperature - 20) * PlotRectangle.Width / 85;
        }

        private int GraphY(int speed)
        {
            return PlotRectangle.Bottom - speed * PlotRectangle.Height / 100;
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
