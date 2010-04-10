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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Shared
{
	internal sealed class MenuHandler : IMenuHandler
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Handle(int tag)
		{
			Handlers handlers;
			
			if (m_handlers.TryGetValue(tag, out handlers))
				handlers.Handler();
			else
				throw new InvalidOperationException(string.Format("Couldn't find a handler for tag {0}", tag));
		}
		
		public MenuState GetState(int tag)
		{
			Handlers handlers;
			
			if (m_handlers.TryGetValue(tag, out handlers))
				return handlers.State();
			
			return 0;
		}
		
		public void Deregister(object owner)
		{
			Contract.Requires(owner != null, "owner is null");
			
			var deathRow = from entry in m_handlers
				where entry.Value.Owner == owner
				select entry.Key;
			
			foreach (int tag in deathRow.ToArray())	// can't use the lazy enumerable if we're deleting
				Unused.Value = m_handlers.Remove(tag);
		}
		
		public void Register(object owner, int tag, Action handler)
		{
			Register(owner, tag, handler, () => true);
		}
		
		public void Register(object owner, int tag, Action handler, Func<bool> enabler)
		{
			Contract.Requires(!m_handlers.ContainsKey(tag), string.Format("a handler for tag {0} already exists", tag));
			
			Func<MenuState> state = () => enabler() ? MenuState.Enabled : MenuState.Disabled;
			m_handlers.Add(tag, new Handlers(owner, handler, state));
		}
		
		public void Register2(object owner, int tag, Action handler, Func<MenuState> state)
		{
			Contract.Requires(!m_handlers.ContainsKey(tag), string.Format("a handler for tag {0} already exists", tag));
			
			m_handlers.Add(tag, new Handlers(owner, handler, state));
		}
		
		#region Private Types
		private struct Handlers
		{
			public Handlers(object owner, Action handler, Func<MenuState> state)
			{
				Contract.Requires(owner != null, "owner is null");
				Contract.Requires(handler != null, "handler is null");
				Contract.Requires(state != null, "state is null");
				
				Owner = owner;
				Handler = handler;
				State = state;
			}
			
			public object Owner {get; private set;}
			public Action Handler {get; private set;}
			public Func<MenuState> State {get; private set;}
		}
		#endregion
	
		#region Fields
		private Boss m_boss;
		private Dictionary<int, Handlers> m_handlers = new Dictionary<int, Handlers>();
		#endregion
	}
}
