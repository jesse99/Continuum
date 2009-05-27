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

using Gear.Helpers;
using MCocoa;
using System;
using System.Linq;

namespace Shared
{
	// Pops up a dialog allowing the user pick an item from a list.
	public class GetItem<T>
	{
		public GetItem()
		{
			Title = "Choose";
			Items = new T[0];
		}
		
		// Window title.
		public string Title {get; set;}
		
		public T[] Items {private get; set;}
		
		// Returns null on cancel.
		public T Run(Func<T, string> key)
		{
			Contract.Requires(key != null, "key is null");
			
			if (ms_controller == null)
				ms_controller = new GetItemController();
				
			T result = default(T);
			
			NSWindow window = ms_controller.window();
			window.setTitle(NSString.Create(Title));	
			ms_controller.Items = (from i in Items select key(i)).ToArray();
			
			string name = ms_controller.Run();
			if (name != null)
				result = Items.First(i => key(i) == name);
			
			return result;
		}
		
		#region Fields
		private static GetItemController ms_controller;
		#endregion
	}
}
