using System;
using System.Drawing;
using System.Windows.Forms;
using Gigasoft.ProEssentials;
using Gigasoft.ProEssentials.Enums;

namespace MultiAxisLayoutExplorer
{
    /// <summary>
    /// ProEssentials WinForms — Multi-Axis Layout Explorer  (.NET 8)
    ///
    /// Code-built WinForms port of the WPF Multi-Axis Layout Explorer.
    /// No .Designer.cs / no .resx — the entire UI is constructed in code so
    /// the Visual Studio WinForms designer is never invoked (it does not load
    /// the native ProEssentials control reliably). Build + F5 works regardless.
    ///
    /// A single Pesgo chart displays engine dyno data (HP, Torque, Temperature,
    /// Pressure vs RPM) across four instantly switchable axis layout modes.
    /// A toggle button moves Pressure to the right Y axis in whichever layout
    /// is active.
    ///
    /// LAYOUT MODES, MIXING METHODS, RIGHT-Y ROUTING:
    ///   See per-method comments below. The ProEssentials chart configuration
    ///   is IDENTICAL to the WPF version — only the host shell changed.
    /// </summary>
    public class MainForm : Form
    {
        // ── ProEssentials chart control (WPF PesgoWpf -> WinForms Pesgo) ──────
        private Pesgo Pesgo1;

        // ── Toolbar buttons (WPF Button -> WinForms Button) ──────────────────
        private Button BtnSeparate;
        private Button BtnOverlapped;
        private Button BtnSplit;
        private Button BtnTwoPerAxis;
        private Button BtnRY;

        // ── Theme colors (WPF SolidColorBrush -> System.Drawing.Color) ────────
        // NOTE: ProEssentials chart properties below still take WPF-style
        // Gigasoft colors via System.Drawing.Color on WinForms — see ColorHP etc.
        static readonly Color UiDarkBg    = Color.FromArgb(0x00, 0x1A, 0x20);
        static readonly Color UiDarkPanel = Color.FromArgb(0x00, 0x2B, 0x35);
        static readonly Color UiAccent    = Color.FromArgb(0x00, 0xE5, 0xE5);
        static readonly Color UiBorder    = Color.FromArgb(0x00, 0x3D, 0x4D);
        static readonly Color UiActiveBg  = Color.FromArgb(0x00, 0x4D, 0x60);
        static readonly Color UiActiveBr  = Color.FromArgb(0x00, 0xFF, 0xFF);
        static readonly Color UiToggleBg  = Color.FromArgb(0x00, 0x40, 0x20);
        static readonly Color UiToggleBr  = Color.FromArgb(0xFF, 0xD2, 0x00);
        static readonly Color UiNormalBg  = Color.FromArgb(0x00, 0x3D, 0x4D);
        static readonly Color UiHoverBg   = Color.FromArgb(0x00, 0x55, 0x66);

        // ── Subset colors — ProEssentials chart palette (UNCHANGED from WPF) ──
        // On WinForms the Gigasoft .NET API uses System.Drawing.Color directly.
        static readonly Color ColorHP     = Color.FromArgb(255,   0, 229, 229); // cyan
        static readonly Color ColorTorque = Color.FromArgb(255,   0, 255,   0); // green
        static readonly Color ColorTemp   = Color.FromArgb(255, 255,  48,  48); // red
        static readonly Color ColorPSI    = Color.FromArgb(255, 255, 210,   0); // gold
        static readonly Color ColorTorqueAlpha = Color.FromArgb(160, 0, 255, 0);

        const int Points = 50;

        int  _currentLayout = 0;
        bool _ryActive      = false;

        Button _activeBtn;

        public MainForm()
        {
            // ── Window properties (WPF Window attrs -> Form properties) ───────
            Text = "ProEssentials — Multi-Axis Layout Explorer";
            ClientSize = new Size(1150, 780);          // WPF Height/Width
            MinimumSize = new Size(750, 520);          // WPF MinHeight/MinWidth (+chrome)
            BackColor = UiDarkBg;
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();

            // WinForms: initialize the chart in Form.Load — by then the form
            // and all child control handles exist and the native PE control is
            // ready. (WPF must instead use the chart control's own Loaded event
            // because the HwndHost'd native window doesn't exist until the
            // control itself loads; WinForms has no such gap.)
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;

            _activeBtn = BtnSeparate;
        }

        // =====================================================================
        // BuildLayout — replaces MainWindow.xaml
        //
        // WPF Grid (Row Auto + Row *) -> WinForms: a Panel docked Top for the
        // toolbar + the chart docked Fill. Add the Fill control FIRST or dock
        // ordering puts the chart under the toolbar incorrectly; WinForms z-order
        // for docking is reverse add-order, so add toolbar AFTER fill control,
        // OR add fill first then bring toolbar to front. We add chart, then
        // toolbar docked Top — toolbar wins the top strip, chart fills the rest.
        // =====================================================================
        private void BuildLayout()
        {
            // ── Chart (WPF Gigasoft:PesgoWpf Grid.Row=1) ──────────────────────
            Pesgo1 = new Pesgo();
            Pesgo1.Dock = DockStyle.Fill;
            Controls.Add(Pesgo1);

            // ── Toolbar panel (WPF Border Grid.Row=0 + StackPanel Horizontal) ─
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiDarkPanel,
                Padding = new Padding(10, 8, 10, 8),
                AutoScroll = false
            };

            // Layout label (WPF TextBlock "AXIS LAYOUT:")
            var lbl = new Label
            {
                Text = "AXIS LAYOUT:",
                ForeColor = Color.FromArgb(0x80, 0x80, 0x80),
                Font = new Font("Consolas", 9f),
                AutoSize = true,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 10, 0)    // top margin 0 — centers in toolbar with buttons
            };

            BtnSeparate   = MakeBtn("\u229F  All Separate",
                "4 independent Y axes, one per series (Examples 012 / 103)", BtnSeparate_Click);
            BtnOverlapped = MakeBtn("\u229E  All Overlapped",
                "All 4 series share one Y region, each with its own scale (Example 103)", BtnOverlapped_Click);
            BtnSplit      = MakeBtn("\u22A0  2 + 2 Split",
                "Two overlapped groups stacked — HP+Torque above, Temp+Pressure below (Example 104)", BtnSplit_Click);
            BtnTwoPerAxis = MakeBtn("\u22A1  2 per Axis",
                "Two axis sections, two series each (Example 013)", BtnTwoPerAxis_Click);
            BtnRY         = MakeBtn("\u21C4  Pressure \u2192 Right Y",
                "Move Pressure (PSI) to the right Y axis in the current layout", BtnRY_Click);

            BtnTwoPerAxis.Margin = new Padding(0, 0, 18, 0); // extra right gap before separator

            // Vertical separator (WPF Border Width=1)
            var sep = new Panel { Width = 1, Height = 24, BackColor = UiBorder,
                                  Margin = new Padding(0, 2, 18, 0) };

            toolbar.Controls.AddRange(new Control[]
            {
                lbl, BtnSeparate, BtnOverlapped, BtnSplit, BtnTwoPerAxis, sep, BtnRY
            });

            Controls.Add(toolbar);          // docked Top, sits above the Fill chart

            ApplyButtonStyle(BtnSeparate, active: true); // initial active state
        }

        // MakeBtn — replaces the WPF LayoutBtn Style + ControlTemplate.
        // FlatStyle.Flat + manual border/hover reproduces the rounded cyan look
        // (WinForms flat buttons can't round corners without owner-draw; the
        // square flat look is the standard WinForms equivalent and is fine).
        private Button MakeBtn(string text, string tip, EventHandler onClick)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 28,                         // uniform height — all toolbar children match
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 9.75f, FontStyle.Bold),
                ForeColor = UiAccent,
                BackColor = UiNormalBg,
                Margin = new Padding(0, 0, 5, 0),    // top margin 0; panel Padding supplies vertical inset
                Padding = new Padding(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderColor = UiAccent;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = UiHoverBg;
            b.Click += onClick;

            var toolTip = new ToolTip();
            toolTip.SetToolTip(b, tip);
            return b;
        }

        // ApplyButtonStyle — replaces SetActiveButton + Style swaps.
        private void ApplyButtonStyle(Button btn, bool active = false, bool toggleOn = false)
        {
            if (toggleOn)
            {
                btn.BackColor = UiToggleBg;
                btn.FlatAppearance.BorderColor = UiToggleBr;
                btn.ForeColor = UiToggleBr;
            }
            else if (active)
            {
                btn.BackColor = UiActiveBg;
                btn.FlatAppearance.BorderColor = UiActiveBr;
                btn.ForeColor = UiActiveBr;
            }
            else
            {
                btn.BackColor = UiNormalBg;
                btn.FlatAppearance.BorderColor = UiAccent;
                btn.ForeColor = UiAccent;
            }
        }

        // =====================================================================
        // MainForm_Load — chart initialization (WinForms Form.Load)
        // BELOW THIS LINE: 100% IDENTICAL to the WPF code-behind. The entire
        // ProEssentials configuration block is framework-agnostic — the same
        // property paths, enums, and arrays work on WinForms and WPF.
        // =====================================================================
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Step 1 — Data: engine dyno sweep, 50 points, RPM 1000–6000
            Pesgo1.PeData.Subsets = 4;
            Pesgo1.PeData.Points  = Points;

            Pesgo1.PeData.DuplicateDataX = DuplicateData.PointIncrement;
            Pesgo1.PeData.X[0, Points - 1] = 0; // pre-allocate X array

            var rand = new Random(17);

            for (int p = 0; p < Points; p++)
            {
                float rpm = 1000f + p * (5000f / (Points - 1));
                float t   = (rpm - 1000f) / 5000f;

                Pesgo1.PeData.X[0, p] = rpm;

                Pesgo1.PeData.Y[0, p] = Math.Max(5f,
                    460f * (float)Math.Pow(t, 0.55) * (1f - 0.05f * (float)Math.Pow(1f - t, 3))
                    + (float)(rand.NextDouble() * 12 - 6));

                Pesgo1.PeData.Y[1, p] =
                    340f * (float)Math.Exp(-Math.Pow((t - 0.56f) / 0.3, 2))
                    + 55f + (float)(rand.NextDouble() * 10 - 5);

                Pesgo1.PeData.Y[2, p] =
                    165f + t * 110f + (float)(rand.NextDouble() * 8 - 4);

                Pesgo1.PeData.Y[3, p] =
                    20f + 26f * (float)Math.Sin(t * Math.PI)
                    + (float)(rand.NextDouble() * 4 - 2);
            }

            // Step 2 — Subset labels and colors
            Pesgo1.PeString.SubsetLabels[0] = "Horsepower (HP)";
            Pesgo1.PeString.SubsetLabels[1] = "Torque (lb-ft)";
            Pesgo1.PeString.SubsetLabels[2] = "Temperature (F)";
            Pesgo1.PeString.SubsetLabels[3] = "Pressure (PSI)";

            Pesgo1.PeColor.SubsetColors[0] = ColorHP;
            Pesgo1.PeColor.SubsetColors[1] = ColorTorqueAlpha;
            Pesgo1.PeColor.SubsetColors[2] = ColorTemp;
            Pesgo1.PeColor.SubsetColors[3] = ColorPSI;

            Pesgo1.PeLegend.SubsetLineTypes[0] = LineType.MediumSolid;
            Pesgo1.PeLegend.SubsetLineTypes[1] = LineType.MediumSolid;
            Pesgo1.PeLegend.SubsetLineTypes[2] = LineType.MediumSolid;
            Pesgo1.PeLegend.SubsetLineTypes[3] = LineType.MediumSolid;

            // Step 3 — Titles, X axis label
            Pesgo1.PeString.MainTitle  = "Engine Dyno — Multi-Axis Layout Explorer";
            Pesgo1.PeString.SubTitle   = "Switch layouts  -  toggle Pressure to right Y  -  drag separator to resize";
            Pesgo1.PeString.XAxisLabel = "RPM";

            // Step 4 — Interaction and zoom
            Pesgo1.PeUserInterface.Allow.Zooming    = AllowZooming.HorzAndVert;
            Pesgo1.PeUserInterface.Allow.ZoomStyle  = ZoomStyle.Ro2Not;
            Pesgo1.PeUserInterface.Allow.ZoomLimits = ZoomLimits.AxisHorizontal;

            Pesgo1.PeUserInterface.Scrollbar.ScrollingHorzZoom = true;
            Pesgo1.PeUserInterface.Scrollbar.ScrollingVertZoom = true;

            Pesgo1.PeUserInterface.Cursor.PromptTracking = true;
            Pesgo1.PeUserInterface.Cursor.PromptLocation = CursorPromptLocation.ToolTip;
            Pesgo1.PeUserInterface.Cursor.PromptStyle    = CursorPromptStyle.XYValues;

            Pesgo1.PePlot.MarkDataPoints = true;
            Pesgo1.PePlot.Option.MinimumPointSize   = MinimumPointSize.Small;
            Pesgo1.PePlot.Option.MaximumPointSize   = MinimumPointSize.Large;
            Pesgo1.PePlot.Option.SolidLineOverArea  = 1;
            Pesgo1.PePlot.Option.FixedLineThickness = true;

            // Step 5 — Style
            Pesgo1.PeColor.BitmapGradientMode = true;
            Pesgo1.PeColor.QuickStyle         = QuickStyle.DarkNoBorder;
            Pesgo1.PeColor.GridBold           = true;
            Pesgo1.PeConfigure.BorderTypes    = TABorder.DropShadow;

            Pesgo1.PeGrid.InFront     = true;
            Pesgo1.PeGrid.LineControl = GridLineControl.Both;
            Pesgo1.PeGrid.Style       = GridStyle.Dot;
            Pesgo1.PeGrid.GridBands   = false;
            Pesgo1.PePlot.DataShadows = DataShadows.Shadows;

            Pesgo1.PeFont.FontSize       = Gigasoft.ProEssentials.Enums.FontSize.Large;
            Pesgo1.PeFont.Fixed          = true;
            Pesgo1.PeFont.MainTitle.Bold = true;

            Pesgo1.PeConfigure.AntiAliasGraphics = true;
            Pesgo1.PeConfigure.RenderEngine      = RenderEngine.Direct2D;
            Pesgo1.PeConfigure.ImageAdjustLeft   = 25;
            Pesgo1.PeConfigure.ImageAdjustRight  = 25;

            // Step 6 — Apply initial layout and render
            ApplyLayout(0);
        }

        // --- ApplyMethods — IDENTICAL to WPF ---
        void ApplyMethods()
        {
            Pesgo1.PePlot.Methods[0] = SGraphPlottingMethods.Bar;            // HP
            Pesgo1.PePlot.Methods[1] = SGraphPlottingMethods.SplineArea;     // Torque
            Pesgo1.PePlot.Methods[2] = SGraphPlottingMethods.PointsPlusLine; // Temp
            Pesgo1.PePlot.Methods[3] = _ryActive
                ? SGraphPlottingMethods.Spline + (int)SGraphPlottingMethods.OnRightAxis
                : SGraphPlottingMethods.Spline;                              // Pressure
        }

        // --- ApplyLayout — IDENTICAL to WPF (Invalidate() works on both) ---
        void ApplyLayout(int layout)
        {
            _currentLayout = layout;

            Pesgo1.PeGrid.MultiAxesSubsets.Clear();
            Pesgo1.PeGrid.OverlapMultiAxes.Clear();
            Pesgo1.PeGrid.MultiAxesProportions.Clear();

            switch (layout)
            {
                case 0: Layout_AllSeparate();   break;
                case 1: Layout_AllOverlapped(); break;
                case 2: Layout_Split2x2();      break;
                case 3: Layout_TwoPerAxis();    break;
            }

            ApplyMethods();
            Pesgo1.PeGrid.WorkingAxis = 0;

            Pesgo1.PeFunction.ReinitializeResetImage();
            Pesgo1.Invalidate();
        }

        // --- Layout_* and ConfigureAxes_4Individual — IDENTICAL to WPF ---
        void Layout_AllSeparate()
        {
            Pesgo1.PeGrid.MultiAxesSubsets[0] = 1;
            Pesgo1.PeGrid.MultiAxesSubsets[1] = 1;
            Pesgo1.PeGrid.MultiAxesSubsets[2] = 1;
            Pesgo1.PeGrid.MultiAxesSubsets[3] = 1;

            Pesgo1.PeGrid.MultiAxesProportions[0] = 0.25f;
            Pesgo1.PeGrid.MultiAxesProportions[1] = 0.25f;
            Pesgo1.PeGrid.MultiAxesProportions[2] = 0.25f;
            Pesgo1.PeGrid.MultiAxesProportions[3] = 0.25f;

            Pesgo1.PeGrid.Option.MultiAxisStyle      = MultiAxisStyle.SeparateAxes;
            Pesgo1.PeGrid.Option.MultiAxesSeparators = MultiAxesSeparators.Medium;
            Pesgo1.PeUserInterface.Allow.MultiAxesSizing = true;

            ConfigureAxes_4Individual();
        }

        void Layout_AllOverlapped()
        {
            Pesgo1.PeGrid.MultiAxesSubsets[0] = 1;
            Pesgo1.PeGrid.MultiAxesSubsets[1] = 1;
            Pesgo1.PeGrid.MultiAxesSubsets[2] = 1;
            Pesgo1.PeGrid.MultiAxesSubsets[3] = 1;

            Pesgo1.PeGrid.OverlapMultiAxes[0] = 4;
            Pesgo1.PeGrid.MultiAxesProportions[0] = 1.0f;

            Pesgo1.PeGrid.Option.MultiAxisStyle    = MultiAxisStyle.GroupAllAxes;
            Pesgo1.PeGrid.Option.AxisNumberSpacing = 2.0;
            Pesgo1.PeUserInterface.Allow.MultiAxesSizing = false;

            ConfigureAxes_4Individual();
        }

        void Layout_Split2x2()
        {
            Pesgo1.PeGrid.MultiAxesSubsets[0] = 1;
            Pesgo1.PeGrid.MultiAxesSubsets[1] = 1;
            Pesgo1.PeGrid.MultiAxesSubsets[2] = 1;
            Pesgo1.PeGrid.MultiAxesSubsets[3] = 1;

            Pesgo1.PeGrid.OverlapMultiAxes[0] = 2;
            Pesgo1.PeGrid.OverlapMultiAxes[1] = 2;
            Pesgo1.PeGrid.MultiAxesProportions[0] = 0.5f;
            Pesgo1.PeGrid.MultiAxesProportions[1] = 0.5f;

            Pesgo1.PeGrid.Option.MultiAxisStyle      = MultiAxisStyle.GroupAllAxes;
            Pesgo1.PeGrid.Option.MultiAxesSeparators = MultiAxesSeparators.Medium;
            Pesgo1.PeUserInterface.Allow.MultiAxesSizing = true;

            ConfigureAxes_4Individual();
        }

        void Layout_TwoPerAxis()
        {
            Pesgo1.PeGrid.MultiAxesSubsets[0] = 2;
            Pesgo1.PeGrid.MultiAxesSubsets[1] = 2;

            Pesgo1.PeGrid.MultiAxesProportions[0] = 0.5f;
            Pesgo1.PeGrid.MultiAxesProportions[1] = 0.5f;

            Pesgo1.PeGrid.Option.MultiAxisStyle      = MultiAxisStyle.SeparateAxes;
            Pesgo1.PeGrid.Option.MultiAxesSeparators = MultiAxesSeparators.Medium;
            Pesgo1.PeUserInterface.Allow.MultiAxesSizing = true;

            Pesgo1.PeGrid.WorkingAxis = 0;
            Pesgo1.PeColor.YAxis        = ColorHP;
            Pesgo1.PeString.YAxisLabel  = "HP / Torque";
            Pesgo1.PeString.RYAxisLabel = "";

            Pesgo1.PeGrid.WorkingAxis = 1;
            Pesgo1.PeColor.YAxis       = ColorTemp;
            Pesgo1.PeString.YAxisLabel = "Temp (F)";

            if (_ryActive)
            {
                Pesgo1.PeColor.RYAxis       = ColorPSI;
                Pesgo1.PeString.RYAxisLabel = "Pressure (PSI)";
            }
            else
            {
                Pesgo1.PeString.RYAxisLabel = "";
            }

            Pesgo1.PeLegend.Style = LegendStyle.OneLineTopOfAxis;
        }

        void ConfigureAxes_4Individual()
        {
            Pesgo1.PeGrid.WorkingAxis = 0;
            Pesgo1.PeColor.YAxis        = ColorHP;
            Pesgo1.PeString.YAxisLabel  = "HP";
            Pesgo1.PeString.RYAxisLabel = "";

            Pesgo1.PeGrid.WorkingAxis = 1;
            Pesgo1.PeColor.YAxis        = ColorTorque;
            Pesgo1.PeString.YAxisLabel  = "Torque (lb-ft)";
            Pesgo1.PeString.RYAxisLabel = "";

            Pesgo1.PeGrid.WorkingAxis = 2;
            Pesgo1.PeColor.YAxis        = ColorTemp;
            Pesgo1.PeString.YAxisLabel  = "Temp (F)";
            Pesgo1.PeString.RYAxisLabel = "";

            Pesgo1.PeGrid.WorkingAxis = 3;
            Pesgo1.PeColor.YAxis       = ColorPSI;
            Pesgo1.PeString.YAxisLabel = "Pressure (PSI)";

            if (_ryActive)
            {
                Pesgo1.PeColor.RYAxis       = ColorPSI;
                Pesgo1.PeString.RYAxisLabel = "Pressure (PSI)";
            }
            else
            {
                Pesgo1.PeString.RYAxisLabel = "";
            }

            Pesgo1.PeLegend.Style = LegendStyle.OneLineTopOfAxis;
        }

        // =====================================================================
        // Button handlers
        // WPF RoutedEventArgs -> WinForms EventArgs. Style swaps -> ApplyButtonStyle.
        // =====================================================================
        void SetActiveButton(Button btn)
        {
            ApplyButtonStyle(_activeBtn, active: false);
            _activeBtn = btn;
            ApplyButtonStyle(btn, active: true);
        }

        private void BtnSeparate_Click(object sender, EventArgs e)
        {
            SetActiveButton(BtnSeparate);
            ApplyLayout(0);
        }

        private void BtnOverlapped_Click(object sender, EventArgs e)
        {
            SetActiveButton(BtnOverlapped);
            ApplyLayout(1);
        }

        private void BtnSplit_Click(object sender, EventArgs e)
        {
            SetActiveButton(BtnSplit);
            ApplyLayout(2);
        }

        private void BtnTwoPerAxis_Click(object sender, EventArgs e)
        {
            SetActiveButton(BtnTwoPerAxis);
            ApplyLayout(3);
        }

        private void BtnRY_Click(object sender, EventArgs e)
        {
            _ryActive = !_ryActive;
            ApplyButtonStyle(BtnRY, toggleOn: _ryActive);
            ApplyLayout(_currentLayout);
        }

        // WinForms Form.FormClosing (WPF Window.Closing -> CancelEventArgs)
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
        }
    }
}
