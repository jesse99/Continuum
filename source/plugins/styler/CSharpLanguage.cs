// Copyright (C) 2009 Jesse Jones
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
using System.Text.RegularExpressions;

namespace Styler
{
	internal sealed class CSharpLanguage : ILanguage, IStyleWith
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public string Name
		{
			get {return "CsLanguage";}
		}
		
		public string FriendlyName
		{
			get {return "c#";}
		}
		
		public string[] Shebangs
		{
			get {return new string[0];}
		}
		
		public int[] TabStops
		{
			get {return m_language.TabStops;}
		}
		
		public bool StylesWhitespace
		{
			get {return m_language != null ? m_language.StylesWhitespace : false;}
		}
		
		public Regex Word
		{
			get {return m_language.Word;}
		}
		
		public string LineComment
		{
			get {return "//";}
		}
		
		public bool UseTabs
		{
			get {return m_language != null ? m_language.UseTabs : false;}
		}
		
		[ThreadModel(ThreadModel.Concurrent)]
		public Language Language
		{
			get {return m_language;}
			set {m_language = value;}
		}
		
		#region Fields
		private Boss m_boss;
		private Language m_language;
		#endregion
	}
}
