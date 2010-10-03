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

using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CsRefactor.Script
{
	internal sealed class MemberType : RefactorType
	{
		private MemberType()
		{
		}
		
		public static MemberType Instance
		{
			get
			{
				if (ms_instance == null)
					ms_instance = new MemberType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return DeclarationType.Instance;}
		}
		
		public override string Name
		{
			get {return "Member";}
		}

		public override Type ManagedType
		{
			get {return typeof(CsMember);}
		}
				
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<CsMember, string>("AddMemberAfter", this.DoAddMethodAfter);
			type.Register<CsMember, string>("AddMemberBefore", this.DoAddMethodBefore);
			type.Register<CsMember, string>("ChangeAccess", this.DoChangeAccess);
			type.Register<CsMember>("get_Access", this.DoGetAccess);
			type.Register<CsMember>("get_Attributes", this.DoGetAttributes);
			type.Register<CsMember>("get_DeclaringType", this.DoGetDeclaringType);
			type.Register<CsMember>("get_FullName", this.DoGetFullName);
			type.Register<CsMember>("get_IsAbstract", this.DoGetIsAbstract);
			type.Register<CsMember>("get_IsConst", this.DoGetIsConst);
			type.Register<CsMember>("get_IsInternal", this.DoGetIsInternal);
			type.Register<CsMember>("get_IsOverride", this.DoGetIsOverride);
			type.Register<CsMember>("get_IsProtected", this.DoGetIsProtected);
			type.Register<CsMember>("get_IsPrivate", this.DoGetIsPrivate);
			type.Register<CsMember>("get_IsPublic", this.DoGetIsPublic);
			type.Register<CsMember>("get_IsReadonly", this.DoGetIsReadonly);
			type.Register<CsMember>("get_IsSealed", this.DoGetIsSealed);
			type.Register<CsMember>("get_IsStatic", this.DoGetIsStatic);
			type.Register<CsMember>("get_IsVirtual", this.DoGetIsVirtual);
			type.Register<CsMember>("get_IsVolatile", this.DoGetIsVolatile);
			type.Register<CsMember>("get_Modifiers", this.DoGetModifiers);
			type.Register<CsMember>("get_Name", this.DoGetName);
		}
		
		#region Private Methods
		private object DoAddMethodAfter(CsMember member, string text)
		{
			return new AddRelativeMember(member, true, text.Split('\n'));
		}
		
		private object DoAddMethodBefore(CsMember member, string text)
		{
			return new AddRelativeMember(member, false, text.Split('\n'));
		}
		
		private object DoChangeAccess(CsMember member, string access)
		{
			return new ChangeAccess(member, access);
		}
		
		private object DoGetAccess(CsMember member)
		{
			return member.Access.ToString().ToLower();
		}
		
		private object DoGetAttributes(CsMember member)
		{
			return member.Attributes;
		}
		
		private object DoGetDeclaringType(CsMember member)
		{
			return member.DeclaringType;
		}
		
		private object DoGetFullName(CsMember member)
		{
			return member.FullName;
		}
		
		private object DoGetIsPublic(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Public) == MemberModifiers.Public;
		}
		
		private object DoGetIsProtected(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Protected) == MemberModifiers.Protected;
		}
		
		private object DoGetIsInternal(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Internal) == MemberModifiers.Internal;
		}
		
		private object DoGetIsPrivate(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Private) == MemberModifiers.Private;
		}
		
		private object DoGetIsStatic(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Static) == MemberModifiers.Static;
		}
		
		private object DoGetIsAbstract(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Abstract) == MemberModifiers.Abstract;
		}
		
		private object DoGetIsVirtual(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Virtual) == MemberModifiers.Virtual;
		}
		
		private object DoGetIsOverride(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Override) == MemberModifiers.Override;
		}
		
		private object DoGetIsSealed(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Sealed) == MemberModifiers.Sealed;
		}
		
		private object DoGetIsReadonly(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Readonly) == MemberModifiers.Readonly;
		}
		
		private object DoGetIsVolatile(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Volatile) == MemberModifiers.Volatile;
		}
		
		private object DoGetIsConst(CsMember member)
		{
			return (member.Modifiers & MemberModifiers.Const) == MemberModifiers.Const;
		}
		
		private object DoGetModifiers(CsMember member)
		{
			return member.Modifiers.ToString().ToLower();
		}
		
		private object DoGetName(CsMember member)
		{
			return member.Name;
		}
		#endregion
		
		private static MemberType ms_instance;
	}
}
