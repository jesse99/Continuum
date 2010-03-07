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
using MCocoa;
using System;
using System.Collections.Generic;

namespace Shared
{
	public sealed class AnnontationContextItem
	{
		public AnnontationContextItem(string name, int state, Action handler)
		{
			Name = name;
			State = state;
			Handler = handler;
		}
		
		// Used for the menu item title. If null the item will be a separator.
		public string Name {get; private set;}
		
		// Returns 0 if the item is unchecked, 1 if it is checked, and -1 if it is
		// checked with the mixed state indicator.
		public int State {get; private set;}
		
		// Called when the item is selected.
		public Action Handler {get; private set;}
		
		public override string ToString()
		{
			return Name ?? "separator";
		}
	}
	
	public enum AnnotationAlignment
	{
		Center,		// the annotation is centered on the line the range starts on
		Top,			// the annotation appears just above the line the range starts on
		Bottom,	// the annotation appears just above the line the range starts on
	}
	
	// A window which looks like a rounded rectangle with a label attached to
	// a live index in a text window.
	public interface ITextAnnotation	// note that this is not a Gear interface
	{
		// Text editor boss.
		Boss Parent {get;}
		
		// Shows/hides the window.
		bool Visible {get; set;}
		
		void Close();
		
		// Returns true if the annotation/text window was closed or the anchor
		// text was deleted.
		bool IsValid {get;}
		
		// The text the annotation is attached to.
		NSRange Anchor {get;}
		
		bool Draggable {get; set;}
		
		// The color of the rounded rectangle.
		NSColor BackColor {get; set;}
		
		// The label text.
		string Text {get; set;}
		
		// The label text.
		NSAttributedString String {get; set;}
		
		// Used to populate the context menu.
		void SetContext(List<AnnontationContextItem> items);
	}
}
