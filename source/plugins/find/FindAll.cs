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

using Gear.Helpers;
using MCocoa;
using MObjc;
//using Shared;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Find
{
	[ExportClass("FindInstance", "NSObject")]
	internal sealed class FindInstance : NSObject
	{
		~FindInstance()
		{
			if ((object) m_context != null)
				m_context.release();
				
			if (m_styledContext != null)
				m_styledContext.release();
		}
		
		public FindInstance(string path, string context, int offset, int index, int length) : base(NSObject.AllocNative("FindInstance"))
		{
			m_path = path;
			m_context = NSString.Create(context).Retain();
			m_range = new NSRange(index, length);
			
			m_styledContext = NSMutableAttributedString.Create(context).Retain();
			NSRange range = new NSRange(index - offset, length);
			m_styledContext.addAttribute_value_range(Externs.NSForegroundColorAttributeName, NSColor.blueColor(), range);
		}
		
		public new FindInstance Retain()
		{
			Unused.Value = retain();
			return this;
		}
		
		public string Path
		{
			get {return m_path;}
		}
			
		public NSString Context
		{
			get {return m_context;}
		}
		
		public NSAttributedString StyledContext
		{
			get {return m_styledContext;}
		}
		
		public NSRange Range
		{
			get {return m_range;}
		}
		
		public void Update(NSRange changed, int delta)
		{		
			if (changed.length == 0 && delta < 0)		// this is a deletion
				changed = new NSRange(changed.location, -delta);
				
			if (changed.location + changed.length < m_range.location)
			{
				m_range.location += delta;
			}	
			else if (m_range.Intersects(changed)) 
			{
				m_range = new NSRange(0, 0);
				
				var attrs = NSDictionary.dictionaryWithObject_forKey(NSColor.disabledControlTextColor(), Externs.NSForegroundColorAttributeName);
				m_styledContext.setAttributes_range(attrs, new NSRange(0, (int) m_context.length()));
			}
		}
		
		private string m_path;
		private NSString m_context;
		private NSRange m_range;
		private NSMutableAttributedString m_styledContext;
	}
	
	[ExportClass("FindsForFile", "NSObject")]
	internal sealed class FindsForFile : NSObject
	{
		~FindsForFile()
		{
			if ((object) m_path != null)
				m_path.release();
				
			if (m_instances != null)
				for (int i = 0; i < m_instances.Length; ++i)
					m_instances[i].release();
				
			if (m_styledPath != null)
				m_styledPath.release();
		}
		
		public FindsForFile(string path, string text, MatchCollection matches) : base(NSObject.AllocNative("FindsForFile"))
		{
			m_path = NSString.Create(path).Retain();
			m_instances = new FindInstance[matches.Count];
			
			int i = 0;
			foreach (Match m in matches)
			{
				int index = m.Index;
				int length = m.Length;
				while (index > 0 && text[index - 1] != '\n')
				{
					--index;
					++length;
				}
				
				while (index + length < text.Length && text[index + length] != '\n')
					++length;
					
				string context = text.Substring(index, length);
				m_instances[i] = new FindInstance(path, context, index, m.Index, m.Length).Retain();
				++i;
			}
			
			var attrs = NSDictionary.dictionaryWithObject_forKey(NSNumber.Create(-3.0f), Externs.NSStrokeWidthAttributeName);
			m_styledPath = NSAttributedString.Create(path, attrs).Retain();
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public new FindsForFile Retain()
		{
			Unused.Value = retain();
			return this;
		}
		
		public NSString Path
		{
			get {return m_path;}
		}
		
		public NSAttributedString StyledPath
		{
			get {return m_styledPath;}
		}
		
		public int Length
		{
			get {return m_instances.Length;}
		}
		
		public FindInstance this[int index]
		{
			get {return m_instances[index];}
		}
		
		private NSString m_path;
		private FindInstance[] m_instances;
		private NSAttributedString m_styledPath;
	}
	
	internal sealed class FindAll : BaseFindInFiles
	{
		~FindAll()
		{
			if (m_finds != null)
				for (int i = 0; i < m_finds.Count; ++i)
					m_finds[i].release();
		}
		
		public FindAll(string directory, Regex re, string[] include, string[] exclude)
			: base(directory, include, exclude)
		{
			m_regex = re;
		}
		
		public FindsForFile[] Results()
		{
			FindsForFile[] results;
			
			lock (m_lock)
			{
				results = m_finds.ToArray();
			}
			
			return results;
		}
		
		#region Protected Methods
		[ThreadModel(ThreadModel.Concurrent)]
		protected override NSFileHandle OnOpenFile(string path)	// threaded
		{
			return NSFileHandle.fileHandleForReadingAtPath(NSString.Create(path));
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		protected override string OnProcessFile(string file, string text)	// threaded
		{
			MatchCollection matches = m_regex.Matches(text);
			if (matches.Count > 0)
			{
				lock (m_lock)
				{
					m_finds.Add(new FindsForFile(file, text, matches).Retain());
				}
			}
			
			return text;
		}
		#endregion
		
		#region Fields
		private Regex m_regex;
		private object m_lock = new object();
		private List<FindsForFile> m_finds = new List<FindsForFile>();
		#endregion
	}
}
