﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using GlazeWM.Domain.Containers;
using GlazeWM.Domain.UserConfigs;
using GlazeWM.Infrastructure.WindowsApi;
using static GlazeWM.Infrastructure.WindowsApi.WindowsApiService;

namespace GlazeWM.Domain.Windows
{
  public class WindowService
  {
    private ContainerService _containerService;
    private UserConfigService _userConfigService;

    public WindowService(ContainerService containerService, UserConfigService userConfigService)
    {
      _containerService = containerService;
      _userConfigService = userConfigService;
    }

    /// <summary>
    /// Get windows by searching entire container forest for Window containers.
    /// </summary>
    public IEnumerable<Window> GetWindows()
    {
      return _containerService.ContainerTree.TraverseDownEnumeration()
        .OfType<Window>();
    }

    /// <summary>
    /// Get windows within given parent container.
    /// </summary>
    public IEnumerable<Window> GetWindowsOfParentContainer(Container parent)
    {
      return parent.TraverseDownEnumeration()
        .OfType<Window>();
    }

    /// <summary>
    /// Get the id of the process that created the window.
    /// </summary>
    public Process GetProcessOfHandle(IntPtr handle)
    {
      uint processId;
      GetWindowThreadProcessId(handle, out processId);
      return Process.GetProcesses().FirstOrDefault(process => process.Id == (int)processId);
    }

    /// <summary>
    /// Get the class name of the specified window.
    /// </summary>
    public string GetClassNameOfHandle(IntPtr handle)
    {
      // Class name is limited to 256 characters, so it's fine to use a fixed size buffer.
      var buffer = new StringBuilder(256);
      GetClassName(handle, buffer, buffer.Capacity);
      return buffer.ToString();
    }

    /// <summary>
    /// Get dimensions of the bounding rectangle of the specified window.
    /// </summary>
    public WindowRect GetLocationOfHandle(IntPtr handle)
    {
      var rect = new WindowRect();
      GetWindowRect(handle, ref rect);
      return rect;
    }

    /// <summary>
    /// Get info about the placement of the specified window.
    /// </summary>
    public WindowPlacement GetPlacementOfHandle(IntPtr handle)
    {
      var windowPlacement = new WindowPlacement();
      GetWindowPlacement(handle, ref windowPlacement);
      return windowPlacement;
    }

    /// <summary>
    /// Get title bar text of the specified window.
    /// </summary>
    public string GetTitleOfHandle(IntPtr handle)
    {
      var titleLength = GetWindowTextLength(handle);

      if (titleLength == 0)
        return String.Empty;

      var buffer = new StringBuilder(titleLength + 1);
      GetWindowText(handle, buffer, buffer.Capacity);
      return buffer.ToString();
    }

    public List<IntPtr> GetAllWindowHandles()
    {
      var windowHandles = new List<IntPtr>();

      EnumWindows((IntPtr hwnd, int lParam) =>
      {
        windowHandles.Add(hwnd);
        return true;
      }, IntPtr.Zero);

      return windowHandles;
    }

    public WS_EX GetWindowStylesEx(IntPtr handle)
    {
      return unchecked((WS_EX)GetWindowLongPtr(handle, (int)(GWL_EXSTYLE)).ToInt64());
    }

    public WS GetWindowStyles(IntPtr handle)
    {
      return unchecked((WS)GetWindowLongPtr(handle, (int)(GWL_STYLE)).ToInt64());
    }

    public bool HandleHasWindowStyle(IntPtr handle, WS style)
    {
      return (GetWindowStyles(handle) & style) != 0;
    }

    public bool HandleHasWindowExStyle(IntPtr handle, WS_EX style)
    {
      return (GetWindowStylesEx(handle) & style) != 0;
    }

    /// <summary>
    /// Whether the given handle is cloaked. For some UWP apps, `WS_VISIBLE` will be true even if
    /// the window isn't actually visible. The `DWMWA_CLOAKED` attribute is used to check whether
    /// these apps are visible.
    /// </summary>
    public bool IsHandleCloaked(IntPtr handle)
    {
      bool isCloaked;
      DwmGetWindowAttribute(handle, DwmWindowAttribute.DWMWA_CLOAKED, out isCloaked, Marshal.SizeOf(typeof(bool)));
      return isCloaked;
    }

    public bool IsWindowManageable(Window window)
    {
      // Get whether window is actually visible.
      var isVisible = IsWindowVisible(window.Hwnd) && !IsHandleCloaked(window.Hwnd);

      if (!isVisible)
        return false;

      // Ensure window is top-level (ie. not a child window). Ignore windows that are probably
      // popups or if they're unavailable in task switcher (alt+tab menu).
      var isApplicationWindow = !window.HasWindowStyle(WS.WS_CHILD)
        && !window.HasWindowExStyle(WS_EX.WS_EX_NOACTIVATE | WS_EX.WS_EX_TOOLWINDOW)
        && GetWindow(window.Hwnd, GW.GW_OWNER) == IntPtr.Zero;

      if (!isApplicationWindow)
        return false;

      // Get whether the window belongs to the current process.
      var isCurrentProcess = window.Process.Id == Process.GetCurrentProcess().Id;

      if (isCurrentProcess)
        return false;

      return true;
    }
  }
}
