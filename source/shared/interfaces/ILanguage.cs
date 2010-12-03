// Copyright (C) 2009-2010 Jesse Jones
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
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Shared
{
	// Primary interface on language bosses.
	public interface ILanguage : IInterface
	{
		// Normally "CsLanguage" or "RegexLanguage".
		string Name {get;}
		
		// Shebangs associated with the language, e.g. "sh", "bash".
		string[] Shebangs {get;}
		
		// "c#", "python", etc.
		string FriendlyName {get;}
		
		// These are the initial tab stops used by text views for this language.
		// Any tabs which appear after these will use the tab interval from the
		// prefs. Note that the stops are expressed as multiples of the tab interval.
		int[] TabStops {get;}
		
		// Returns true if the language supports showing leading/trailing tabs and
		// spaces.
		bool StylesWhitespace {get;}
		
		// Returns a regex used to define what the language considers a word (for
		// things like double clicking).
		Regex Word {get;}
		
		// Used by toggle comment command. May be null.
		string LineComment {get;}
	}
}
