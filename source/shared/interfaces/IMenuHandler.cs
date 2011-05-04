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
using Gear.Helpers;
using System;

namespace Shared
{
	[Flags]
	public enum MenuState
	{
		Disabled = 0,
		Enabled = 1,
		Checked = 2,
	}
	
	// Used to attach menu handlers to objects in the responder chain. Currently these
	// can be attached to the app via appHandle, the directory editor via dirHandler,
	// or text editors via textHandler.
	public interface IMenuHandler : IInterface
	{
		void Handle(int tag);
		
		MenuState GetState(int tag);
		
		// Handler is always enabled.
		void Register(object owner, int tag, Action handler);
		
		void Register(object owner, int tag, Action handler, Func<bool> enabler);
		
		void Register2(object owner, int tag, Action handler, Func<MenuState> state);
		
		// Removes every handler owner registered.
		[ThreadModel(ThreadModel.Concurrent)]
		void Deregister(object owner);
	}
}
