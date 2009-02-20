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
using System;
using System.Collections.Generic;

namespace Shared
{	
	public sealed class TextContextItem
	{
		public TextContextItem(float sortOrder)
			: this(null, null, sortOrder, null)
		{
		}
		
		public TextContextItem(string name, Func<string, string> handler, float sortOrder)
			: this(name, handler, sortOrder, null)
		{
		}
		
		public TextContextItem(string name, Func<string, string> handler, float sortOrder, string undo)
			: this(name, handler, sortOrder, undo, null)
		{
		}
		
		public TextContextItem(string name, Func<string, string> handler, float sortOrder, string undo, NSAttributedString title)
		{
			Name = name;
			Handler = handler;
			SortOrder = sortOrder;
			UndoText = undo;
			Title = title;
		}
		
		// Used for the menu item title. If null the item will be a separator.
		public string Name {get; private set;}
		
		// The handler will be passed the original selection and can either return a new value
		// which will replace the original selection or the input selection.
		public Func<string, string> Handler {get; private set;}
		
		// Lower values are placed towards the start of the menu. The standard commands
		// are in [0.1, 0.9] and are multiples of 0.1. Use 0.5 if you don't care where the
		// item lands.
		public float SortOrder {get; private set;}
		
		// If null, Name will be used instead.
		public string UndoText {get; private set;}
		
		// If not null this will be used for the menu item title. Note that this should be
		// in the autorelease pool.
		public NSAttributedString Title {get; private set;}

		public override string ToString()
		{
			return Name ?? "separator";
		}
	}

	// Repeated interface on TextEditorPlugin used to generate the context
	// menu for text views.
	public interface ITextContextCommands : IInterface
	{
		// Adds zero or more menu items the list. Note that the selection may
		// be null (if there is no selection) but will not be empty. Boss will be
		// the directory editor boss.
		void Get(Boss boss, string selection, List<TextContextItem> items);		
	} 
}
