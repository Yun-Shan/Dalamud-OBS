using Dalamud.Plugin.Services;
using Newtonsoft.Json.Linq;
using OBSPlugin.Objects;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OBSPlugin
{
    public class BlurManager
    {
        private readonly IOBSWebsocket _obs;
        private readonly Configuration _config;
        private bool _isThreadRunning = true;

        internal BlockingCollection<Blur> BlurItemsToAdd { get; } = new(10000);
        internal BlockingCollection<Blur> BlurItemsToRemove { get; } = new(10000);
        public Dictionary<string, Blur> BlurDict { get; } = new();

        public BlurManager(IOBSWebsocket obs, Configuration config)
        {
            _obs = obs;
            _config = config;
            InitAddConsuming();
            InitRemoveConsuming();
        }

        private void InitAddConsuming()
        {
            Task.Run(() =>
            {
                while (!BlurItemsToAdd.IsCompleted && _isThreadRunning)
                {
                    Blur? blur = null;
                    try
                    {
                        blur = BlurItemsToAdd.Take();
                    }
                    catch (InvalidOperationException) { }

                    if (blur != null)
                    {
                        if (BlurDict.TryGetValue(blur.Name, out Blur? latestBlur))
                        {
                            if (blur.LastEdit.CompareTo(latestBlur.LastEdit) < 0)
                            {
                                continue;
                            }

                        }
                        OBSAddOrUpdateBlur(blur);
                    }
                }
                Svc.PluginLog.Information("No more OBS blurs to add.");
            });

        }

        private void InitRemoveConsuming()
        {
            Task.Run(() =>
            {
                while (!BlurItemsToRemove.IsCompleted && _isThreadRunning)
                {
                    Blur? blur = null;
                    try
                    {
                        blur = BlurItemsToRemove.Take();
                    }
                    catch (InvalidOperationException) { }

                    if (blur != null)
                    {
                        OBSRemoveBlur(blur);
                    }
                }
                Svc.PluginLog.Information("No more OBS blurs to add.");
            });
        }

        internal bool OBSAddOrUpdateBlur(Blur blur)
        {
            if (!_obs.IsConnected) return false;

            string sourceName = _config.SourceName;
            try
            {
                FilterSettings? filter = null;
                var settings = new JObject();
                bool created = false;
                try
                {
                    filter = _obs.GetSourceFilter(sourceName, blur.Name);
                    settings = filter.Settings;
                }
                catch
                {
                    created = true;
                }

                /* obs-composite-blur settings*/
                settings["blur_algorithm"] = 3; //ALGO_DUAL_KAWASE
                settings["blur_type"] = 1;
                settings["effect_mask"] = 1; //EFFECT_MASK_TYPE_CROP
                settings["effect_mask_crop_top"] = blur.Top;
                settings["effect_mask_crop_bottom"] = blur.Bottom;
                settings["effect_mask_crop_left"] = blur.Left;
                settings["effect_mask_crop_right"] = blur.Right;
                settings["kawase_passes"] = blur.Size;


                if (created)
                {
                    _obs.CreateSourceFilter(sourceName, blur.Name, "obs_composite_blur", settings);
                }
                else
                {
                    _obs.SetSourceFilterSettings(sourceName, blur.Name, settings);
                }
                _obs.SetSourceFilterEnabled(sourceName, blur.Name, blur.Enabled);
            }
            catch (ErrorResponseException e)
            {
                if (e.ToString().Contains("specified source doesn't exist"))
                {
                    _config.UIDetection = false;
                    var errMsg = $"Cannot find source \"{_config.SourceName}\", please check.";
                    Svc.PluginLog.Error(errMsg);
                    _config.Save();
                }
                return false;
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Failed updating blur: {0}", e);
                return false;
            }
            Svc.PluginLog.Debug("Updated blur: {0} {1} ({2}, {3}, {4}, {5})", blur.Name, blur.Enabled, blur.Top, blur.Bottom, blur.Left, blur.Right);
            return true;
        }

        internal bool OBSRemoveBlur(Blur blur)
        {
            if (!_obs.IsConnected) return false;
            bool removed;
            try
            {
                removed = _obs.RemoveSourceFilter(_config.SourceName, blur.Name);
                Svc.PluginLog.Debug("Deleted blur: {0}", blur.Name);
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Failed deleting blur: {0}", e);
                return false;
            }
            return removed;
        }

        internal bool OBSRemoveBlurs(string blurNamePrefix)
        {
            if (!_obs.IsConnected) return false;
            try
            {
                var filters = _obs.GetSourceFilterList(_config.SourceName);
                foreach (var filter in filters)
                {
                    if (filter.Name.StartsWith(blurNamePrefix))
                    {
                        _obs.RemoveSourceFilter(_config.SourceName, filter.Name);
                    }
                }
                Svc.PluginLog.Debug("Deleted all blurs starting with {0}", blurNamePrefix);
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Failed deleting blurs: {0}", e);
                return false;
            }
            return true;
        }

        internal void UpdateBlur(Blur blur)
        {
            blur.LastEdit = DateTime.Now;
            if (BlurDict.TryGetValue(blur.Name, out Blur? existingBlur))
            {
                if (!blur.Equals(existingBlur))
                {
                    BlurDict[blur.Name] = blur;
                    if (_config.BlurAsync)
                    {
                        BlurItemsToAdd.Add(blur);
                    }
                    else
                    {
                        OBSAddOrUpdateBlur(blur);
                    }
                }
            }
            else
            {
                BlurDict[blur.Name] = blur;
                BlurItemsToAdd.Add((Blur)blur.Clone());
            }
        }

        public void Dispose()
        {
            _isThreadRunning = false;
            BlurItemsToAdd.CompleteAdding();
            BlurItemsToRemove.CompleteAdding();
        }
    }
}