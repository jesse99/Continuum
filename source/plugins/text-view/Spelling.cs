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
using System.Collections.Generic;
using System.Linq;

// Allow deprecated methods so that we can continue to run on leopard.
#pragma warning disable 618

namespace TextView
{
	internal sealed class Spelling : ITextContextCommands
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Get(string selection, string language, bool editable, List<TextContextItem> items)
		{
			if (selection != null && editable)
			{
				if (NSApplication.sharedApplication().Call("canLookupInDictionary:", NSString.Create(selection)).To<bool>())
				{
					items.Add(new TextContextItem("Look Up in Dictionary", this.DoFindInDict, 0.11f));
				}
				
				if (DoNeedsSpellCheck(selection))
				{
					NSRange range = NSSpellChecker.sharedSpellChecker().checkSpellingOfString_startingAt(NSString.Create(selection), 0);
					if (range.length > 0)
					{
						items.Add(new TextContextItem(0.9f));
						
						string word = selection.Substring(range.location, range.length);
						NSArray guesses = NSSpellChecker.sharedSpellChecker().guessesForWord(NSString.Create(word));
						if (guesses.count() > 0)
						{
							for (int i = 0; i < guesses.count(); ++i)
							{
								string guess = guesses.objectAtIndex((uint) i).description();
								items.Add(new TextContextItem(guess, s => guess, 0.9f, "Spelling Correction"));
							}
						}
					}
				}
			}
		}
		
		#region Private Methods
		private string DoFindInDict(string text)
		{
			NSURL url = NSURL.URLWithString(NSString.Create("dict:///" + text.Replace(" ", "%20")));
			NSWorkspace.sharedWorkspace().openURL(url);
			
			return text;
		}
		
		private bool DoNeedsSpellCheck(string selection)
		{
			// If the selection is large or has multiple words then spell check it.
			if (selection.Length > 100)
				return true;
			
			for (int i = 0; i < selection.Length; ++i)
			{
				if (char.IsWhiteSpace(selection[i]))
					return true;
			}
			
			// If the selection is one word and has an interior upper case letter
			// or underscore then don't spell check it.
			for (int i = 1; i < selection.Length; ++i)
			{
				if (selection[i] == '_' || char.IsUpper(selection[i]))
					return false;
			}
			
			// Otherwise spell check it.
			return true;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
