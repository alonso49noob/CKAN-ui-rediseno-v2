using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics.CodeAnalysis;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

using log4net;

namespace CKAN.GUI
{
    /// <summary>
    /// Thumbnails for the mod list, taken from the image SpaceDock indexes for
    /// each mod.
    ///
    /// Fetching is driven by painting, so only the mods actually on screen are
    /// ever downloaded. Everything is cached twice over: scaled thumbnails on
    /// disk so a restart costs nothing, and decoded bitmaps in memory so
    /// scrolling doesn't touch the disk.
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    [ExcludeFromCodeCoverage]
    public static class ModIcons
    {
        /// <summary>Side of the square thumbnail, in pixels</summary>
        public const int Size = 24;

        /// <summary>
        /// The thumbnail for a mod, or null when there isn't one yet. A miss
        /// starts a download and repaints the grid once it lands, so callers
        /// just draw nothing this time round.
        /// </summary>
        public static Bitmap? Get(GUIMod mod, Control repaintOnArrival)
        {
            var identifier = mod.Identifier;
            if (cache.TryGetValue(identifier, out var cached))
            {
                return cached;
            }
            if (mod.Module.resources?.xScreenshot is not Uri url)
            {
                // Nothing to fetch; remember that so we don't keep asking
                cache[identifier] = null;
                return null;
            }
            Fetch(identifier, url, repaintOnArrival);
            return null;
        }

        private static void Fetch(string identifier, Uri url, Control grid)
        {
            if (!inFlight.TryAdd(identifier, true))
            {
                return;
            }
            Task.Run(() =>
            {
                Bitmap? thumbnail = null;
                try
                {
                    thumbnail = FromDisk(identifier) ?? FromNetwork(identifier, url);
                }
                catch (Exception exc)
                {
                    // A broken image or an unreachable host is not worth
                    // bothering the user about; the mod just has no thumbnail
                    log.DebugFormat("No thumbnail for {0}: {1}", identifier, exc.Message);
                }
                cache[identifier] = thumbnail;
                inFlight.TryRemove(identifier, out _);
                if (thumbnail != null)
                {
                    Repaint(grid);
                }
            });
        }

        private static Bitmap? FromDisk(string identifier)
        {
            var path = CachePath(identifier);
            if (!File.Exists(path))
            {
                return null;
            }
            // Copy it out so the file isn't locked for the life of the process
            using (var stream = File.OpenRead(path))
            using (var loaded = new Bitmap(stream))
            {
                return new Bitmap(loaded);
            }
        }

        private static Bitmap? FromNetwork(string identifier, Uri url)
        {
            var temp = Path.GetTempFileName();
            try
            {
                Net.Download(url, out _, null, temp);
                using (var stream = File.OpenRead(temp))
                using (var full   = new Bitmap(stream))
                {
                    var thumbnail = Scale(full);
                    var path      = CachePath(identifier);
                    if (Path.GetDirectoryName(path) is string dir)
                    {
                        Directory.CreateDirectory(dir);
                    }
                    thumbnail.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    return thumbnail;
                }
            }
            finally
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException)
                {
                }
            }
        }

        /// <summary>
        /// Crops to a square from the middle before scaling, so a wide
        /// screenshot doesn't end up as a squashed letterbox.
        /// </summary>
        private static Bitmap Scale(Bitmap source)
        {
            var side   = Math.Min(source.Width, source.Height);
            var square = new Rectangle((source.Width  - side) / 2,
                                       (source.Height - side) / 2,
                                       side, side);
            var scaled = new Bitmap(Size, Size);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, Size, Size),
                            square, GraphicsUnit.Pixel);
            }
            return scaled;
        }

        /// <summary>
        /// Repaint at most a few times a second: a screenful of thumbnails
        /// arriving at once would otherwise queue up a repaint for each.
        /// </summary>
        private static void Repaint(Control grid)
        {
            if (Interlocked.Exchange(ref repaintPending, 1) == 1)
            {
                return;
            }
            try
            {
                grid.BeginInvoke(new Action(() =>
                {
                    Interlocked.Exchange(ref repaintPending, 0);
                    if (!grid.IsDisposed)
                    {
                        grid.Invalidate();
                    }
                }));
            }
            catch (Exception)
            {
                // Grid went away, or its handle isn't up yet
                Interlocked.Exchange(ref repaintPending, 0);
            }
        }

        private static string CachePath(string identifier)
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "CKAN", "logos", $"{identifier}.{Size}.png");

        private static readonly ConcurrentDictionary<string, Bitmap?> cache    = new ConcurrentDictionary<string, Bitmap?>();
        private static readonly ConcurrentDictionary<string, bool>    inFlight = new ConcurrentDictionary<string, bool>();
        private static          int                                   repaintPending;

        private static readonly ILog log = LogManager.GetLogger(typeof(ModIcons));
    }
}
