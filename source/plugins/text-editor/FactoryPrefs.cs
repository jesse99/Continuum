// Copyright (C) 2008 Jesse Jones
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
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Diagnostics;

namespace TextEditor
{
	internal sealed class Attributes
	{
		public string Font {get; set;}
		
		public float Size {get; set;}
		
		public int[] Color {get; set;}
		
		public int[] BackColor {get; set;}
		
		public int Underline {get; set;}
	}
	
	internal sealed class FactoryPrefs : IFactoryPrefs
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void OnInitFactoryPref(NSMutableDictionary dict)
		{
			dict.setObject_forKey(NSNumber.Create(true), NSString.Create("show spaces"));
			dict.setObject_forKey(NSNumber.Create(true), NSString.Create("show tabs"));
			
			DoInitStyle(dict, "text default", new Attributes{Font = "Verdana", Size = 14.0f, Color = new int[]{0, 0, 0}});
			DoInitStyle(dict, "text keyword", new Attributes{Font = "Verdana-Bold", Size = 14.0f, Color = new int[]{7, 91, 255}});
			DoInitStyle(dict, "text type", new Attributes{Font = "Verdana-Bold", Size = 18.0f, Color = new int[]{0, 0, 0}});
			DoInitStyle(dict, "text member", new Attributes{Font = "Verdana-Bold", Size = 14.0f, Color = new int[]{0, 0, 0}});
			DoInitStyle(dict, "text string", new Attributes{Font = "Verdana", Size = 14.0f, Color = new int[]{175, 47, 127}});
			DoInitStyle(dict, "text number", new Attributes{Font = "Verdana", Size = 14.0f, Color = new int[]{155, 102, 83}});
			DoInitStyle(dict, "text comment", new Attributes{Font = "Verdana-Italic", Size = 14.0f, Color = new int[]{239, 15, 46}});
			DoInitStyle(dict, "text preprocess", new Attributes{Font = "Verdana-BoldItalic", Size = 18.0f, Color = new int[]{131, 11, 253}});
			DoInitStyle(dict, "text other1", new Attributes{Font = "Verdana-Bold", Size = 14.0f, Color = new int[]{44, 96, 67}});
			DoInitStyle(dict, "text other2", new Attributes{Font = "Verdana-BoldItalic", Size = 18.0f, Color = new int[]{131, 11, 253}});
			
			DoInitStyle(dict, "text spaces", new Attributes{BackColor = new int[]{255, 227, 227}});
			DoInitStyle(dict, "text tabs", new Attributes{BackColor = new int[]{227, 255, 255}});
			
			NSColor color = NSColor.colorWithDeviceRed_green_blue_alpha(239/255.0f, 255/255.0f, 252/255.0f, 1.0f);
			NSData data = NSArchiver.archivedDataWithRootObject(color);
			dict.setObject_forKey(data, NSString.Create("text default color"));
			
			color = NSColor.colorWithDeviceRed_green_blue_alpha(255/255.0f, 255/255.0f, 177/255.0f, 1.0f);
			data = NSArchiver.archivedDataWithRootObject(color);
			dict.setObject_forKey(data, NSString.Create("selected line color"));
			
			dict.setObject_forKey(NSNumber.Create(20.0f), NSString.Create("tab stops"));
			
			var paths = NSMutableArray.Create("/System/Library/", "/usr/include/");
			dict.setObject_forKey(paths, NSString.Create("preferred paths"));
		}
		
		#region Private Methods
		private void DoInitStyle(NSMutableDictionary dict, string name, Attributes a)
		{
			if (a.Font != null)
			{
				Contract.Assert(a.Size > 0.0, "size is zero");
				
				dict.setObject_forKey(NSString.Create(a.Font), NSString.Create(name + " font name"));
				dict.setObject_forKey(NSNumber.Create(a.Size), NSString.Create(name + " font size"));
			}
			
			if (a.Underline != 0)
			{
				var attrs = NSMutableDictionary.Create();
				attrs.setObject_forKey(NSNumber.Create(a.Underline), Externs.NSUnderlineStyleAttributeName);
				
				if (a.Color != null)
				{
					Contract.Assert(a.Color.Length == 3, "color does not have three components");
					Contract.Assert(a.BackColor == null, "we don't support setting both the fore and back colors");
					
					NSColor color = NSColor.colorWithDeviceRed_green_blue_alpha(a.Color[0]/255.0f, a.Color[1]/255.0f, a.Color[2]/255.0f, 1.0f);
					attrs.setObject_forKey(color, Externs.NSUnderlineColorAttributeName);
				}
				
				var data = NSArchiver.archivedDataWithRootObject(attrs);
				dict.setObject_forKey(data, NSString.Create(name + " font attributes"));
			}
			else if (a.Color != null)
			{
				Contract.Assert(a.Color.Length == 3, "color does not have three components");
				Contract.Assert(a.BackColor == null, "we don't support setting both the fore and back colors");
				
				NSColor color = NSColor.colorWithDeviceRed_green_blue_alpha(a.Color[0]/255.0f, a.Color[1]/255.0f, a.Color[2]/255.0f, 1.0f);
				var attrs = NSDictionary.dictionaryWithObject_forKey(color, Externs.NSForegroundColorAttributeName);
				var data = NSArchiver.archivedDataWithRootObject(attrs);
				dict.setObject_forKey(data, NSString.Create(name + " font attributes"));
			}
			else if (a.BackColor != null)
			{
				Contract.Assert(a.BackColor.Length == 3, "back color does not have three components");
				
				NSColor color = NSColor.colorWithDeviceRed_green_blue_alpha(a.BackColor[0]/255.0f, a.BackColor[1]/255.0f, a.BackColor[2]/255.0f, 1.0f);
				var data = NSArchiver.archivedDataWithRootObject(color);
				dict.setObject_forKey(data, NSString.Create(name + " color"));
				
				var attrs = NSDictionary.dictionaryWithObject_forKey(color, Externs.NSBackgroundColorAttributeName);
				data = NSArchiver.archivedDataWithRootObject(attrs);
				dict.setObject_forKey(data, NSString.Create(name + " color attributes"));
			}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	} 
}
