using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;

namespace NPPopupMenu
{
    public class PopupEx : Popup
    {
        public static readonly DependencyProperty CloseOnMouseLeftButtonDownProperty
            = DependencyProperty.Register(nameof(CloseOnMouseLeftButtonDown),
                typeof(bool),
                typeof(PopupEx),
                new PropertyMetadata(false));

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private bool? appliedTopMost;

        private Window hostWindow;

        public PopupEx()
        {
            Loaded += PopupEx_Loaded;
            Opened += PopupEx_Opened;
        }

        /// <summary>
        ///     Gets/sets if the popup can be closed by left mouse button down.
        /// </summary>
        public bool CloseOnMouseLeftButtonDown
        {
            get { return (bool) GetValue(CloseOnMouseLeftButtonDownProperty); }
            set { SetValue(CloseOnMouseLeftButtonDownProperty, value); }
        }

        /// <summary>
        ///     Causes the popup to update it's position according to it's current settings.
        /// </summary>
        public void RefreshPosition()
        {
            var offset = HorizontalOffset;
            // "bump" the offset to cause the popup to reposition itself on its own
            SetCurrentValue(HorizontalOffsetProperty, offset + 1);
            SetCurrentValue(HorizontalOffsetProperty, offset);
        }

        private void PopupEx_Loaded(object sender, RoutedEventArgs e)
        {
            var target = PlacementTarget as FrameworkElement;
            if (target == null)
            {
                return;
            }

            hostWindow = Window.GetWindow(target);
            if (hostWindow == null)
            {
                return;
            }

            hostWindow.LocationChanged -= hostWindow_SizeOrLocationChanged;
            hostWindow.LocationChanged += hostWindow_SizeOrLocationChanged;
            hostWindow.SizeChanged -= hostWindow_SizeOrLocationChanged;
            hostWindow.SizeChanged += hostWindow_SizeOrLocationChanged;
            target.SizeChanged -= hostWindow_SizeOrLocationChanged;
            target.SizeChanged += hostWindow_SizeOrLocationChanged;
            hostWindow.StateChanged -= hostWindow_StateChanged;
            hostWindow.StateChanged += hostWindow_StateChanged;
            hostWindow.Activated -= hostWindow_Activated;
            hostWindow.Activated += hostWindow_Activated;
            hostWindow.Deactivated -= hostWindow_Deactivated;
            hostWindow.Deactivated += hostWindow_Deactivated;

            Unloaded -= PopupEx_Unloaded;
            Unloaded += PopupEx_Unloaded;
        }

        private void PopupEx_Opened(object sender, EventArgs e)
        {
            SetTopmostState(true);
        }

        private void hostWindow_Activated(object sender, EventArgs e)
        {
            SetTopmostState(true);
        }

        private void hostWindow_Deactivated(object sender, EventArgs e)
        {
            SetTopmostState(false);
        }

        private void PopupEx_Unloaded(object sender, RoutedEventArgs e)
        {
            var target = PlacementTarget as FrameworkElement;
            if (target != null)
            {
                target.SizeChanged -= hostWindow_SizeOrLocationChanged;
            }
            if (hostWindow != null)
            {
                hostWindow.LocationChanged -= hostWindow_SizeOrLocationChanged;
                hostWindow.SizeChanged -= hostWindow_SizeOrLocationChanged;
                hostWindow.StateChanged -= hostWindow_StateChanged;
                hostWindow.Activated -= hostWindow_Activated;
                hostWindow.Deactivated -= hostWindow_Deactivated;
            }
            Unloaded -= PopupEx_Unloaded;
            Opened -= PopupEx_Opened;
            hostWindow = null;
        }

        private void hostWindow_StateChanged(object sender, EventArgs e)
        {
            if (hostWindow != null && hostWindow.WindowState != WindowState.Minimized)
            {
                // special handling for validation popup
                var target = PlacementTarget as FrameworkElement;
                var holder = target != null ? target.DataContext as AdornedElementPlaceholder : null;
                if (holder != null && holder.AdornedElement != null)
                {
                    PopupAnimation = PopupAnimation.None;
                    IsOpen = false;
                    var errorTemplate = holder.AdornedElement.GetValue(Validation.ErrorTemplateProperty);
                    holder.AdornedElement.SetValue(Validation.ErrorTemplateProperty, null);
                    holder.AdornedElement.SetValue(Validation.ErrorTemplateProperty, errorTemplate);
                }
            }
        }

        private void hostWindow_SizeOrLocationChanged(object sender, EventArgs e)
        {
            RefreshPosition();
        }

        private void SetTopmostState(bool isTop)
        {
            // Don抰 apply state if it抯 the same as incoming state
            if (appliedTopMost.HasValue && appliedTopMost == isTop)
            {
                return;
            }

            if (Child == null)
            {
                return;
            }

            var hwndSource = PresentationSource.FromVisual(Child) as HwndSource;
            if (hwndSource == null)
            {
                return;
            }
            var hwnd = hwndSource.Handle;

            RECT rect;
            if (!GetWindowRect(hwnd, out rect))
            {
                return;
            }
            //Debug.WriteLine("setting z-order " + isTop);

            var left = rect.Left;
            var top = rect.Top;
            var width = rect.Width;
            var height = rect.Height;
            if (isTop)
            {
                SetWindowPos(hwnd, HWND_TOPMOST, left, top, width, height, SWP.TOPMOST);
            }
            else
            {
                // Z-Order would only get refreshed/reflected if clicking the
                // the titlebar (as opposed to other parts of the external
                // window) unless I first set the popup to HWND_BOTTOM
                // then HWND_TOP before HWND_NOTOPMOST
                SetWindowPos(hwnd, HWND_BOTTOM, left, top, width, height, SWP.TOPMOST);
                SetWindowPos(hwnd, HWND_TOP, left, top, width, height, SWP.TOPMOST);
                SetWindowPos(hwnd, HWND_NOTOPMOST, left, top, width, height, SWP.TOPMOST);
            }

            appliedTopMost = isTop;
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (CloseOnMouseLeftButtonDown)
            {
                IsOpen = false;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal static int LOWORD(int i)
        {
            return (short) (i & 0xFFFF);
        }

        [SecurityCritical]
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [SecurityCritical]
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
            SWP uFlags);

        [SecurityCritical]
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        private static bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SWP uFlags)
        {
            if (!_SetWindowPos(hWnd, hWndInsertAfter, x, y, cx, cy, uFlags))
            {
                // If this fails it's never worth taking down the process.  Let the caller deal with the error if they want.
                return false;
            }

            return true;
        }

        /// <summary>
        ///     SetWindowPos options
        /// </summary>
        [Flags]
        internal enum SWP
        {
            ASYNCWINDOWPOS = 0x4000,
            DEFERERASE = 0x2000,
            DRAWFRAME = 0x0020,
            FRAMECHANGED = 0x0020,
            HIDEWINDOW = 0x0080,
            NOACTIVATE = 0x0010,
            NOCOPYBITS = 0x0100,
            NOMOVE = 0x0002,
            NOOWNERZORDER = 0x0200,
            NOREDRAW = 0x0008,
            NOREPOSITION = 0x0200,
            NOSENDCHANGING = 0x0400,
            NOSIZE = 0x0001,
            NOZORDER = 0x0004,
            SHOWWINDOW = 0x0040,
            TOPMOST = NOACTIVATE | NOOWNERZORDER | NOSIZE | NOMOVE | NOREDRAW | NOSENDCHANGING
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
            public void Offset(int dx, int dy)
            {
                Left += dx;
                Top += dy;
                Right += dx;
                Bottom += dy;
            }

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
            public int Left { get; set; }

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
            public int Right { get; set; }

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
            public int Top { get; set; }

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
            public int Bottom { get; set; }

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
            public int Width => Right - Left;

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
            public int Height => Bottom - Top;

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
            public POINT Position => new POINT {x = Left, y = Top};

            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
            public SIZE Size => new SIZE {cx = Width, cy = Height};

            public static RECT Union(RECT rect1, RECT rect2)
            {
                return new RECT
                {
                    Left = Math.Min(rect1.Left, rect2.Left),
                    Top = Math.Min(rect1.Top, rect2.Top),
                    Right = Math.Max(rect1.Right, rect2.Right),
                    Bottom = Math.Max(rect1.Bottom, rect2.Bottom)
                };
            }

            public override bool Equals(object obj)
            {
                try
                {
                    var rc = (RECT) obj;
                    return rc.Bottom == Bottom
                           && rc.Left == Left
                           && rc.Right == Right
                           && rc.Top == Top;
                }
                catch (InvalidCastException)
                {
                    return false;
                }
            }

            public override int GetHashCode()
            {
                return ((Left << 16) | LOWORD(Right)) ^ ((Top << 16) | LOWORD(Bottom));
            }
        }
    }
}