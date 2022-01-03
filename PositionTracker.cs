/* Copyright (C) 2021 - Mywk.Net
 * Licensed under the EUPL, Version 1.2
 * You may obtain a copy of the Licence at: https://joinup.ec.europa.eu/community/eupl/og_page/eupl
 * Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 */
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace NWSMM
{
    public class PositionTracker
    {

        /// <summary>
        /// A very QND way to fix the returned string - This can/should be cleaned up and optimized
        /// </summary>
        /// <param name="toFix"></param>
        /// <returns></returns>
        private static string FixFloatString(string toFix)
        {
            toFix = toFix.Replace(" ", "");

            if (toFix.Contains("."))
                toFix = toFix.Replace(",", "");
            else
                toFix = toFix.Replace(",", ".").Replace(" ", ".");

            while (toFix.Contains(".."))
                toFix = toFix.Replace("..", ".");

            while (toFix.EndsWith("."))
                toFix = toFix.Remove(toFix.Length - 1);

            if (toFix.Contains("."))
                toFix = toFix.Remove(toFix.IndexOf('.'));

            if (toFix.Length > 5 || toFix.Length < 3)
                return "";

            return toFix;
        }

        // Invalidate the previous position after X false positions (eg. Teleport)
        private const int MaxCounter = 50;

        private float _lastX;
        private float _lastY;
        private DateTime _lastValidPosition = DateTime.Now;
        private int _invalidPositionCounter = MaxCounter;

        /// <summary>
        /// Pre-process a position from the given text without updating the position
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public Vector2 PositionFromText(string text)
        {
            Vector2 position = default;

            Regex PosRegex = new Regex(@"(\d+\d+(\,|\.|\s))(.?)(\d+\d+(\,|\.|\s))", RegexOptions.Compiled);

            var matches = PosRegex.Matches(text);

            if (matches.Count >= 2)
            {
                string xValue = FixFloatString(matches[0].Groups[0].Value);
                string yValue = FixFloatString(matches[1].Groups[0].Value);

                if (xValue == "" || yValue == "")
                    return default;

                float x = float.Parse(xValue);
                float y = float.Parse(yValue);
                position = new Vector2(x, y);
            }

            return position;
        }

        /// <summary>
        /// Checks if the given text contains a position and updates accordingly if that's the case
        /// </summary>
        /// <param name="position"></param>
        /// <remarks>Some if the calculations were kindly copied from NewWorldMinimap since it saves me a bit of time</remarks>
        /// <returns>True if the position is valid and was updated</returns>
        public bool UpdatePosition(ref Vector2 position)
        {
            float x = position.X;
            float y = position.Y;

            x %= 100000;

            while (x > 14260)
                x -= 10000;

            y %= 10000;

            if (_invalidPositionCounter >= MaxCounter)
                _invalidPositionCounter = 0;
            else
            {
                if ((Math.Abs(_lastX - x) > 30 || Math.Abs(_lastY - y) > 30) && _invalidPositionCounter < MaxCounter)
                {
                    x = _lastX;
                    y = _lastY;
                    _invalidPositionCounter++;
                    return false;
                }
            }

            if (x >= 4468 && x <= 14260 && y >= 84 && y <= 9999)
            {
                _lastX = x;
                _lastY = y;
                position = new Vector2(x, y);
                _lastValidPosition = DateTime.Now;
                return true;
            }

            position = default;
            return false;
        }

    }
}
