/* MacroRepeater.cs -- sends macros that have a repeat interval again and
   again.

   Copyright (C) 2025-2026 NoGraphic999.  This file is part of 4Term, released
   under the MIT License; see the LICENSE file for details.  */

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _4Term
{
    /* The repeating macros.  A macro with a repeat interval (set in the
       macro buffer) starts repeating when its button is clicked and stops
       on the next click.  Each repeat sends the macro's saved command, so
       saving a new command changes what a running repeat sends.  */
    internal sealed class MacroRepeater : IDisposable
    {
        /* Sends COMMAND once; false if the port is closed.  */
        private readonly Func<string, bool> Send;
        /* Timers of the repeating macros, by macro number.  */
        private readonly Dictionary<int, Timer> Timers = new Dictionary<int, Timer>();

        /* Raised when a macro starts or stops repeating.  */
        public event Action Changed;

        /* Create a repeater that sends through SEND.  */
        public MacroRepeater(Func<string, bool> send)
        {
            Send = send;
        }

        /* True while macro MACRO repeats.  */
        public bool IsRunning(int macro) => Timers.ContainsKey(macro);

        /* Number of repeating macros.  */
        public int Count => Timers.Count;

        /* Start repeating macro MACRO every INTERVAL milliseconds, sending
           it at once too.  */
        public void Start(int macro, int interval)
        {
            Stop(macro);
            /* The timer's Tag is the macro it repeats, so that Move can
               point it at a new slot.  */
            var timer = new Timer { Interval = Math.Max(1, interval), Tag = macro };
            timer.Tick += (s, e) => Tick((int)((Timer)s).Tag);
            Timers[macro] = timer;
            timer.Start();
            Changed?.Invoke();
            Tick(macro);
        }

        /* Stop repeating macro MACRO.  */
        public void Stop(int macro)
        {
            if (!Timers.TryGetValue(macro, out Timer timer))
                return;
            timer.Stop();
            timer.Dispose();
            Timers.Remove(macro);
            Changed?.Invoke();
        }

        /* Stop every repeat, as when the port closes.  */
        public void StopAll()
        {
            foreach (int macro in new List<int>(Timers.Keys))
                Stop(macro);
        }

        /* Bring the running repeats up to date with the saved macros: those
           that may no longer repeat stop, the others use their new
           interval.  */
        public void Refresh()
        {
            foreach (int macro in new List<int>(Timers.Keys))
            {
                int interval = IntervalOf(macro);
                if (interval <= 0)
                    Stop(macro);
                else
                    Timers[macro].Interval = interval;
            }
        }

        /* Follow macros that moved to other slots: NEWSLOT returns where
           the macro of a slot went, or -1 if it is gone.  Repeats of gone
           macros stop.  */
        public void Move(Func<int, int> newSlot)
        {
            var moved = new Dictionary<int, Timer>();
            foreach (KeyValuePair<int, Timer> entry in Timers)
            {
                int target = newSlot(entry.Key);
                if (target < 0 || moved.ContainsKey(target))
                {
                    entry.Value.Stop();
                    entry.Value.Dispose();
                    continue;
                }
                entry.Value.Tag = target;
                moved[target] = entry.Value;
            }
            Timers.Clear();
            foreach (KeyValuePair<int, Timer> entry in moved)
                Timers[entry.Key] = entry.Value;
            Changed?.Invoke();
        }

        /* Send macro MACRO's saved command; stop if it may no longer
           repeat or the port is closed.  */
        private void Tick(int macro)
        {
            string command = (Settings.MacroCommand.Text[macro] ?? "").Trim();
            if (IntervalOf(macro) <= 0 || !Send(command))
                Stop(macro);
        }

        /* The repeat interval of macro MACRO in milliseconds, or 0 if it
           does not repeat: it has no interval or command, or it runs a
           script or a risky command, which never repeat (config.xml may
           have been edited by hand).  */
        public static int IntervalOf(int macro)
        {
            string text = (Settings.MacroRepeat.Interval[macro] ?? "").Trim();
            string command = (Settings.MacroCommand.Text[macro] ?? "").Trim();
            if (command.Length == 0 || command.StartsWith(">", StringComparison.Ordinal) ||
                MacroBufferPane.IsRisky(command))
                return 0;
            return int.TryParse(text, out int interval) && interval >= 50 ? interval : 0;
        }

        public void Dispose() => StopAll();
    }
}
