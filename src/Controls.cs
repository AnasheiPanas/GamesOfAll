using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GamesOfAll
{
    internal enum PillStyle
    {
        Chip,
        Accent,
        Ghost,
        Close
    }

    // Boton redondeado usado para filtros, acciones y controles de ventana.
    internal sealed class PillButton : Control
    {
        public PillStyle Style = PillStyle.Chip;
        public bool Selected;
        public string Icon;
        public bool IconOnly;

        private bool hover;
        private bool down;

        public PillButton(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            TabStop = false;
            Font = Theme.BodyBold;
            Height = Theme.Px(32);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float radius = Theme.Pxf(Style == PillStyle.Close ? 7f : 9f);
            Color textColor = Theme.TextDim;

            if (Style == PillStyle.Accent)
            {
                Theme.FillRoundGradient(g, r, radius,
                    down ? Theme.AccentDeep : Theme.Accent,
                    down ? Theme.Accent : Theme.Accent2);
                textColor = Color.FromArgb(10, 14, 20);
            }
            else if (Style == PillStyle.Close)
            {
                if (hover)
                {
                    Theme.FillRound(g, r, radius, Color.FromArgb(48, 255, 90, 90));
                    textColor = Color.FromArgb(255, 190, 190);
                }
            }
            else
            {
                if (Selected)
                {
                    Theme.FillRound(g, r, radius, Color.FromArgb(28, 38, 50));
                    Theme.StrokeRound(g, r, radius, Color.FromArgb(70, Theme.Accent), 1.2f);
                    textColor = Theme.Accent;
                }
                else if (hover)
                {
                    Theme.FillRound(g, r, radius, down ? Theme.SurfaceHover : Theme.Surface);
                    textColor = Theme.Text;
                }
            }

            string label = IconOnly ? "" : Text;
            int iconWidth = string.IsNullOrEmpty(Icon) ? 0 : Theme.Px(15);
            Size textSize = string.IsNullOrEmpty(label)
                ? Size.Empty
                : TextRenderer.MeasureText(label, Font, new Size(Width, Height), TextFormatFlags.NoPadding);
            int spacing = (iconWidth > 0 && textSize.Width > 0) ? Theme.Px(7) : 0;
            int total = iconWidth + spacing + textSize.Width;
            int x = (Width - total) / 2;

            if (iconWidth > 0)
            {
                TextRenderer.DrawText(g, Icon, Theme.IconGlyph, new Rectangle(x, 0, iconWidth, Height), textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                x += iconWidth + spacing;
            }
            if (!string.IsNullOrEmpty(label))
            {
                TextRenderer.DrawText(g, label, Font, new Rectangle(x, 0, textSize.Width + 2, Height), textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }
    }

    // Caja de busqueda con esquinas redondeadas y texto de ayuda.
    internal sealed class SearchBox : Control
    {
        private const int EM_SETCUEBANNER = 0x1501;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private readonly TextBox inner;
        private readonly string placeholder;
        private readonly string hint;
        private bool clearHover;

        public event EventHandler QueryChanged;

        public SearchBox(string placeholder, string hint)
        {
            this.placeholder = placeholder;
            this.hint = hint;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.BgSoft;

            inner = new TextBox();
            inner.BorderStyle = BorderStyle.None;
            inner.BackColor = Theme.Surface;
            inner.ForeColor = Theme.Text;
            inner.Font = Theme.Body;
            inner.TabStop = true;
            inner.TextChanged += delegate { Invalidate(); if (QueryChanged != null) QueryChanged(this, EventArgs.Empty); };
            inner.GotFocus += delegate { Invalidate(); };
            inner.LostFocus += delegate { Invalidate(); };
            Controls.Add(inner);
        }

        public string Query
        {
            get { return inner.Text.Trim(); }
        }

        public void Clear()
        {
            if (inner.Text.Length > 0) inner.Text = "";
            else if (QueryChanged != null) QueryChanged(this, EventArgs.Empty);
        }

        public void FocusInput()
        {
            inner.Focus();
            inner.SelectAll();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SendMessage(inner.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int padLeft = Theme.Px(36);
            int padRight = Theme.Px(34);
            int h = Theme.Px(20);
            inner.SetBounds(padLeft, (Height - h) / 2, Math.Max(20, Width - padLeft - padRight), h);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = e.X >= Width - Theme.Px(30);
            if (over != clearHover) { clearHover = over; Cursor = over ? Cursors.Hand : Cursors.IBeam; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            clearHover = false; Cursor = Cursors.IBeam; Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.X >= Width - Theme.Px(30)) Clear();
            else FocusInput();
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Theme.FillRound(g, r, Theme.Pxf(9), Theme.Surface);
            Theme.StrokeRound(g, r, Theme.Pxf(9),
                inner.Focused ? Color.FromArgb(90, Theme.Accent) : Theme.Stroke, 1f);

            Color glyph = inner.Focused ? Theme.Accent : Theme.TextFaint;
            TextRenderer.DrawText(g, "\uE721", Theme.IconGlyph,
                new Rectangle(Theme.Px(12), 0, Theme.Px(18), Height), glyph,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (inner.Text.Length > 0)
            {
                Color closeColor = clearHover ? Theme.Text : Theme.TextFaint;
                TextRenderer.DrawText(g, "\u2715", Theme.Small,
                    new Rectangle(Width - Theme.Px(30), 0, Theme.Px(22), Height), closeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(22, 26, 33); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(38, 45, 57); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(38, 45, 57); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(38, 45, 57); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(30, 36, 45); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(30, 36, 45); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(48, 56, 70); } }
        public override Color MenuBorder { get { return Color.FromArgb(45, 52, 64); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(22, 26, 33); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(22, 26, 33); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(22, 26, 33); } }
        public override Color CheckBackground { get { return Color.FromArgb(48, 60, 76); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(60, 74, 92); } }
        public override Color SeparatorDark { get { return Color.FromArgb(40, 46, 57); } }
        public override Color SeparatorLight { get { return Color.FromArgb(26, 31, 39); } }
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled
                ? (e.Item.Selected ? Theme.Text : Color.FromArgb(214, 221, 232))
                : Theme.TextFaint;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen p = new Pen(Color.FromArgb(52, 60, 74)))
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextDim;
            base.OnRenderArrow(e);
        }
    }

    internal static class Menus
    {
        public static ContextMenuStrip Create()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.BackColor = Color.FromArgb(22, 26, 33);
            menu.ForeColor = Color.FromArgb(214, 221, 232);
            menu.ShowImageMargin = false;
            menu.Font = Theme.Body;
            menu.Padding = new Padding(0, Theme.Px(5), 0, Theme.Px(5));
            return menu;
        }

        public static ToolStripMenuItem Item(ContextMenuStrip menu, string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = Color.FromArgb(214, 221, 232);
            item.BackColor = Color.FromArgb(22, 26, 33);
            item.Padding = new Padding(Theme.Px(4), Theme.Px(3), Theme.Px(4), Theme.Px(3));
            if (handler != null) item.Click += handler;
            menu.Items.Add(item);
            return item;
        }

        public static void Separator(ContextMenuStrip menu)
        {
            menu.Items.Add(new ToolStripSeparator());
        }
    }
}
