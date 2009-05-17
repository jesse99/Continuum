// Copyright (C) 2007-2008 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Gear;
using Gear.Helpers;
using Shared;
using System;
using System.IO;

namespace App
{
	internal sealed class TimeMachineWindowTitle : IDocumentWindowTitle
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public string GetTitle(string displayName)
		{
			string title = displayName;
			
			var editor = m_boss.Get<ITextEditor>();
			if (editor.Path != null)
			{
				DateTime old = File.GetLastWriteTime(editor.Path);
				TimeSpan age = DateTime.Now - old;
				title += string.Format(" (from {0})", AgeToString(age));
			}
			
			return title;
		}
		
		internal static string AgeToString(TimeSpan delta)
		{
			const int Day = 24*1;
			const int Month = 30*Day;
			const int Year = 12*Month;
			
			double hours = delta.TotalHours;
			
			var builder = new System.Text.StringBuilder();
			DoAppend(builder, ref hours, Year, "year");
			DoAppend(builder, ref hours, Month, "month");
			DoAppend(builder, ref hours, Day, "day");
			DoAppend(builder, ref hours, 1, "hour");
			
			builder.Append("ago");
			
			return builder.ToString();
		}
		
		#region Private Methods
		private static void DoAppend(System.Text.StringBuilder builder, ref double hours, int units, string name)
		{
			int count = (int) (hours/units);
			if (count > 0)
			{
				builder.Append(count.ToString());
				builder.Append(' ');
				builder.Append(name);
				if (count != 1)
					builder.Append('s');
				builder.Append(' ');
				
				hours -= count*units;
			}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
