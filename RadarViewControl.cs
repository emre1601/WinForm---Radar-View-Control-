using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Emre1601;
/*****************************
 * Emre1601 RadarViewControl
 * 
 * Geliştirici: Emre1601
 * GitHub:https://github.com/emre1601
 * ***************************/
public class RadarViewControl : Control
{


    #region HelperFunctions
    private double Normalize(double value, double min, double max) => (value - min) / (max - min);
    private Color InvertColor(Color color) => Color.FromArgb(color.A, 255 - color.R, 255 - color.G, 255 - color.B);
    public string GetUnitString(UnitTypes unit, bool shortName = false)
    {
        return unit switch
        {
            UnitTypes.KiloMeter => shortName ? "KM" : "Kilometer",
            UnitTypes.Meter => shortName ? "M" : "Meter",
            UnitTypes.CentiMeter => shortName ? "CM" : "Centimeter",
            _ => ""
        };
    }


    #endregion
    #region Properties & Fields

    private double _rotationOffset = 0;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public double RotationOffset
    {
        get { return _rotationOffset; }
        set { _rotationOffset = (value % 360 + 360) % 360; }
    } // İleride kullanmak üzere döndürme ofseti ekleyebilirsin

    public enum UnitTypes { KiloMeter, Meter, CentiMeter }


    [Category("Radar Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color RadarDegTextColor { get; set; } = Color.Gray;
    [Category("Radar Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color RadarColor { get; set; } = Color.Green;
    [Category("Radar Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color RadarLineColor { get; set; } = Color.Lime;
    [Category("Radar Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool IsDrawStatics { get; set; } = true;



    private UnitTypes _unit = UnitTypes.Meter;
    [Category("Radar Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public UnitTypes Unit { get => _unit; set { _unit = value; Invalidate(); } }

    private double _angleDeg = 0;
    [Category("Radar Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public double AngleDeg
    {
        get => _angleDeg; set
        {

            _angleDeg = (value % 360 + 360) % 360;
            UpdateLastPositions(); // Noktayı burada ekliyoruz
                                   //değiştirmeden önceki son target pointi temizliyorum.
            if (AutoClearTargetPoints)
            {
                Guid[] guids = TargetPoints.Where(tp => tp.Angle == _angleDeg).Select(tp => tp.Id).ToArray();

                foreach (var guid in guids)
                {
                    RemoveTargetPointWithGuid(guid);
                }
            }



            Invalidate();
        }
    }

    private double _radarRange = 100;
    [Category("Radar Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public double RadarRange { get => _radarRange; set { _radarRange = value; Invalidate(); } }

    private double _radarCurrentDistance = 0;
    [Category("Radar Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public double RadarCurrentDistance
    {
        get => _radarCurrentDistance; set
        {
            _radarCurrentDistance = Math.Clamp(value, 0, RadarRange);
            UpdateLastPositions(); // Mesafe değişince de güncelleyebilirsin
            Invalidate();
        }
    }

    [Category("Radar Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool ShowLastPositions { get; set; }

    [Category("Radar Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public byte LastPositionCount { get; set; } = 1;

    [Category("Radar Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public float DefaultTargetPointSize { get; set; } = 4f;
    [Category("Radar Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool AutoClearTargetPoints { get; set; } = true;
    public struct LastPositionData
    {
        public Point Position { get; set; }
        public DateTime Timestamp { get; set; }
        public double Distance { get; set; }
    }
    public struct TargetPoint
    {
        public required Guid Id { get; init; }
        public double Distance { get; set; }
        public float Size { get; set; }
        public DateTime Timestamp { get; set; }
        public Color TargetColor { get; set; }
        public string Label { get; set; }
        public double Angle { get; set; }

        public override string ToString()
        {
            string result = $"Angle: {Angle}°\nDistance: {Distance}\nDate: {Timestamp.ToString("d/MM/yyyy H:mm:ss")}";
            return result;
        }

    }


    private List<TargetPoint> _targetPoints = new List<TargetPoint>();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Category("Radar Data")]
    public List<TargetPoint> TargetPoints
    {
        get => _targetPoints;
        set
        {
            _targetPoints = value ?? new List<TargetPoint>();
        }
    }

    public void ClearTargetPoints() => _targetPoints.Clear();
    public void RemoveTargetPoint(int index)
    {
        _targetPoints.RemoveAt(index);
    }
    public void RemoveTargetPointWithGuid(Guid guid)
    {
        for (int i = 0; i < _targetPoints.Count; i++)
        {
            if (_targetPoints[i].Id == guid)
            {
                _targetPoints.RemoveAt(i);
                break;
            }
        }
    }


    public void AddTargetPoint(double angleDeg, double distance) => AddTargetPoint(angleDeg, distance, DefaultTargetPointSize, RadarLineColor);
    public void AddTargetPoint(double angleDeg, double distance, float size) => AddTargetPoint(angleDeg, distance, size, RadarLineColor);
    public void AddTargetPoint(double angleDeg, double distance, Color color) => AddTargetPoint(angleDeg, distance, DefaultTargetPointSize, color);
    public void AddTargetPoint(double angleDeg, double distance, float size, Color color)
    {
        TargetPoint newTarget = new TargetPoint()
        {
            Id = Guid.NewGuid(),
            Distance = distance,
            Size = size,
            Timestamp = DateTime.Now,
            TargetColor = color, // Örnek renki 
            Angle = angleDeg
        };
        _targetPoints.Add(newTarget);
    }
    public TargetPoint CreateTargetPoint(double angle, double distance)
    {
        return new TargetPoint()
        {
            Id = Guid.NewGuid(),
            Distance = distance,
            Size = DefaultTargetPointSize,// default boyut
            Timestamp = DateTime.Now,
            TargetColor = RadarLineColor, // default renk
            Angle = angle
        };
    }

    private Point AngleToScreenPosition(double angleDeg, double distance)
    {
        Point center = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
        float rx = center.X - 40;
        float ry = center.Y - 40;
        double angleRad = (angleDeg + RotationOffset) * Math.PI / 180;
        double lengthFactor = Normalize(distance, 0, RadarRange);

        int endX = center.X + (int)((lengthFactor * rx) * Math.Cos(angleRad));
        int endY = center.Y - (int)((lengthFactor * ry) * Math.Sin(angleRad));

        return new Point(endX, endY);
    }

    public List<LastPositionData> LastPositions { get; private set; } = new List<LastPositionData>();


    private bool _isMouseIn = false;
    private Point _mousePos = Point.Empty;

    #endregion

    public RadarViewControl()
    {
        // Titremeyi önlemek için kritik ayarlar
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint |
                 ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = true;

    }

    #region Mouse Events

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isMouseIn = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isMouseIn = false; Invalidate(); }

    private TargetPoint? hoverTargetPoint = null;
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mousePos = e.Location;

        hoverTargetPoint = null;

        foreach (var targetpoint in TargetPoints)
        {
            //targetpoint.Position


            PointF TargetPos = AngleToScreenPosition(targetpoint.Angle, targetpoint.Distance);
            TargetPos.X -= targetpoint.Size;
            TargetPos.Y -= targetpoint.Size;

            RectangleF targetPointRect = new RectangleF(TargetPos, new SizeF(targetpoint.Size * 2, targetpoint.Size * 2));
            if (targetPointRect.Contains(_mousePos))
            {

                hoverTargetPoint = targetpoint;
                break;
            }

        }

    }

    #endregion


    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        // --- Grafik Kalite Ayarları ---
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(BackColor);

        // --- Temel Geometri ---
        Point center = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
        float rx = center.X - 40; // X yarıçapı
        float ry = center.Y - 40; // Y yarıçapı
        double angleRad = (AngleDeg + RotationOffset) * Math.PI / 180;
        double lengthFactor = Normalize(RadarCurrentDistance, 0, RadarRange);

        // --- Çizim Katmanları ---
        if (IsDrawStatics) DrawStatistics(g);

        DrawBackgroundGrid(g, center, rx, ry);

        DrawDegrees(g, center, rx, ry);


        DrawTargetPoints(g);

        DrawRadarNeedle(g, center, rx, ry, angleRad, lengthFactor);
        if (hoverTargetPoint.HasValue) DrawMouseTooltip(g);

        g.DrawString("Emre1601 Radar UI", new Font("Arial", 8), new SolidBrush(InvertColor(BackColor)), new Point(0, Height - 10));

        //if (_isMouseIn) DrawMouseTooltip(g);
    }

    private void DrawTargetPoints(Graphics g)
    {
        foreach (var targetPoint in _targetPoints)
        {
            Point targetScreenPosition = AngleToScreenPosition(targetPoint.Angle, (int)targetPoint.Distance);
            g.FillEllipse(new SolidBrush(targetPoint.TargetColor), targetScreenPosition.X - (targetPoint.Size / 2), targetScreenPosition.Y - (targetPoint.Size / 2), targetPoint.Size, targetPoint.Size);

        }

        if (hoverTargetPoint.HasValue)
        {
            Point targetScreenPosition = AngleToScreenPosition(hoverTargetPoint.Value.Angle, (int)hoverTargetPoint.Value.Distance);
            g.FillEllipse(new SolidBrush(InvertColor(hoverTargetPoint.Value.TargetColor)), targetScreenPosition.X - (hoverTargetPoint.Value.Size / 2), targetScreenPosition.Y - (hoverTargetPoint.Value.Size / 2), hoverTargetPoint.Value.Size, hoverTargetPoint.Value.Size);
        }
    }

    private void DrawStatistics(Graphics g)
    {
        using var font = new Font("Consolas", 8);
        using var brush = new SolidBrush(ForeColor);
        string unitStr = GetUnitString(Unit, true);

        string info = $"Max Distance: {RadarRange:F2} {unitStr}\n" +
                      $"Current Dist: {RadarCurrentDistance:F2} {unitStr}\n" +
                      $"Angle: {MathF.Abs(Convert.ToSingle(AngleDeg))}";

        g.DrawString(info, font, brush, 5, 5);
    }

    private void DrawBackgroundGrid(Graphics g, Point center, float rx, float ry)
    {
        var rect = new RectangleF(center.X - rx, center.Y - ry, rx * 2, ry * 2);

        // Arkaplan Dolgusu (Yarı saydam)
        using (var fillBrush = new SolidBrush(Color.FromArgb(80, RadarColor)))
            g.FillEllipse(fillBrush, rect);

        // Ana Dış Çerçeve
        using (var mainPen = new Pen(RadarColor, 2))
            g.DrawEllipse(mainPen, rect);

        // İç Halkalar
        using (var subPen = new Pen(RadarColor, 1))
        {
            for (int i = 1; i <= 3; i++) // 4. halka zaten dış çerçeve
            {
                float step = i / 4f;
                float w = rx * 2 * step;
                float h = ry * 2 * step;
                g.DrawEllipse(subPen, center.X - w / 2, center.Y - h / 2, w, h);
            }
        }
    }

    private void DrawDegrees(Graphics g, Point center, float rx, float ry)
    {
        using var font = new Font("Arial", 10, FontStyle.Bold);
        using var brush = new SolidBrush(RadarDegTextColor);

        using var shadowBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
        for (int i = 0; i < 360; i += 30)
        {
            double rad = (i + RotationOffset) * Math.PI / 180;
            float x = center.X + (float)(rx * Math.Cos(rad));
            float y = center.Y - (float)(ry * Math.Sin(rad));

            string val = i.ToString();
            var size = g.MeasureString(val, font);

            //Text Shadow
            g.DrawString(val, font, shadowBrush, x - (size.Width / 2) + 1.5f, y - (size.Height / 2) + 1.5f);

            //text
            g.DrawString(val, font, brush, x - (size.Width / 2), y - (size.Height / 2));

        }
    }
    // Yardımcı metod:
    private void UpdateLastPositions()
    {
        if (ClientSize.Width == 0 || ClientSize.Height == 0) return;
        Point center = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
        float rx = center.X - 40;
        float ry = center.Y - 40;
        double angleRad = (AngleDeg + RotationOffset) * Math.PI / 180;
        double lengthFactor = Normalize(RadarCurrentDistance, 0, RadarRange);

        int endX = center.X + (int)((lengthFactor * rx) * Math.Cos(angleRad));
        int endY = center.Y - (int)((lengthFactor * ry) * Math.Sin(angleRad));

        Point newPoint = new Point(endX, endY);

        // Eğer son nokta ile aynı değilse ekle (gereksiz kalabalıktan kaçınmak için)
        if (LastPositions.Count == 0 || LastPositions.Last().Position != newPoint)
        {
            LastPositions.Add(new LastPositionData() { Position = newPoint, Distance = RadarCurrentDistance, Timestamp = DateTime.Now });
            if (LastPositions.Count > LastPositionCount)
                LastPositions.RemoveAt(0);
        }
    }
    private void DrawRadarNeedle(Graphics g, Point center, float rx, float ry, double angleRad, double lengthFactor)
    {
        int endX = center.X + (int)((lengthFactor * rx) * Math.Cos(angleRad));
        int endY = center.Y - (int)((lengthFactor * ry) * Math.Sin(angleRad));

        // Merkezdeki Nokta
        using (var brush = new SolidBrush(RadarLineColor))
            g.FillEllipse(brush, center.X - 8, center.Y - 8, 16, 16);

        // Radar Çizgisi
        using (var pen = new Pen(RadarLineColor, 4))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.ArrowAnchor;
            g.DrawLine(pen, center.X, center.Y, endX, endY);


        }
        if (ShowLastPositions && LastPositions.Count > 1)
        {
            GraphicsPath gp = new GraphicsPath();
            for (int i = 0; i < LastPositions.Count; i++)
            {
                if (i == 0)
                    gp.AddLine(new Point(center.X, center.Y), LastPositions[i].Position);
                else
                    gp.AddLine(LastPositions[i - 1].Position, LastPositions[i].Position);
            }



            g.FillPath(new SolidBrush(Color.FromArgb(80, RadarLineColor)), gp);
            g.DrawPath(new Pen(Color.FromArgb(200, RadarLineColor), 1), gp);

        }
    }

    private void DrawMouseTooltip(Graphics g)
    {
        string posText = $"{(string.IsNullOrEmpty(hoverTargetPoint.Value.Label) ? hoverTargetPoint.Value : hoverTargetPoint.Value.Label)}";
        using var font = new Font("Consolas", 8);
        var size = g.MeasureString(posText, font);

        var rect = new RectangleF(_mousePos.X + 15, _mousePos.Y + 15, size.Width + 4, size.Height + 4);

        using (var bgBrush = new SolidBrush(Color.FromArgb(180, Color.Black)))
            g.FillRoundedRectangle(bgBrush, rect, new SizeF(8, 8));

        g.DrawString(posText, font, Brushes.White, rect.X + 2, rect.Y + 2);
    }
}
