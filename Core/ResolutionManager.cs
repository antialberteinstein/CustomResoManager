using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CustomResoManager.Models;

namespace CustomResoManager.Core
{
    public interface IResolutionManager
    {
        ResolutionModel GetCurrentResolution();
        List<ResolutionModel> GetSupportedResolutions();
        bool ChangeResolution(int width, int height);
        void RestoreNativeResolution();
    }

    public class ResolutionManager : IResolutionManager
    {
        private ResolutionModel? _nativeResolution;

        public ResolutionManager()
        {
            // Usually the original desktop resolution loaded at startup is considered "Native"
            _nativeResolution = GetCurrentResolution();
            _nativeResolution.IsNative = true;
        }

        /// <summary>
        /// Gets the current resolution of the display.
        /// </summary>
        public ResolutionModel GetCurrentResolution()
        {
            try
            {
                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                if (NativeMethods.EnumDisplaySettings(null, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    return new ResolutionModel
                    {
                        Width = devMode.dmPelsWidth,
                        Height = devMode.dmPelsHeight,
                        RefreshRate = devMode.dmDisplayFrequency,
                        IsNative = false
                    };
                }
                throw new Exception("Unable to get current display settings.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in GetCurrentResolution: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Retrieves all supported resolutions for the primary display.
        /// </summary>
        public List<ResolutionModel> GetSupportedResolutions()
        {
            var resolutions = new List<ResolutionModel>();
            try
            {
                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                int modeNum = 0;
                while (NativeMethods.EnumDisplaySettings(null, modeNum, ref devMode))
                {
                    resolutions.Add(new ResolutionModel
                    {
                        Width = devMode.dmPelsWidth,
                        Height = devMode.dmPelsHeight,
                        RefreshRate = devMode.dmDisplayFrequency,
                        IsNative = false
                    });
                    modeNum++;
                }

                // Remove duplicates (ignoring bits per pixel for this context, but grouping by primary specs)
                var uniqueResolutions = resolutions
                    .GroupBy(r => new { r.Width, r.Height, r.RefreshRate })
                    .Select(g => g.First())
                    .OrderByDescending(r => r.Width)
                    .ThenByDescending(r => r.Height)
                    .ToList();

                return uniqueResolutions;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in GetSupportedResolutions: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Changes the current resolution.
        /// </summary>
        public bool ChangeResolution(int width, int height)
        {
            try
            {
                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                // First get current settings to preserve correct refresh rate etc.
                if (!NativeMethods.EnumDisplaySettings(null, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    throw new Exception("Could not get current display settings before changing.");
                }

                devMode.dmPelsWidth = width;
                devMode.dmPelsHeight = height;
                devMode.dmFields = NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT;

                // Attempt to change resolution temporarily
                int result = NativeMethods.ChangeDisplaySettings(ref devMode, 0); // No CDS_UPDATEREGISTRY flag so it doesn't persist across reboots

                if (result == NativeMethods.DISP_CHANGE_SUCCESSFUL)
                {
                    return true;
                }
                else
                {
                    throw new Exception($"Failed to change display settings. Error Code: {result}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in ChangeResolution: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Restores the resolution to the initial native native resolution retrieved when class was instantiated.
        /// </summary>
        public void RestoreNativeResolution()
        {
            if (_nativeResolution != null)
            {
                ChangeResolution(_nativeResolution.Width, _nativeResolution.Height);
            }
        }
    }
}