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

namespace CsParser
{
	// Note that this parser is a bit simplified because we only care whether the text is well-formed
	// not if it is actually correct.
	public sealed class Parser
	{
		public Parser(string text)
		{
			m_text = text;
			m_scanner = new Scanner();
			m_scanner.Init(text);
		}
		
		public CsGlobalNamespace Parse()
		{
			m_try = false;
			return DoParseCompilationUnit();
		}
		
		// Note that we take some pains to recover from parser errors. This is important
		// because they are quite common while code is being edited and we don't want
		// to lose all information about a type just because a brace is missing.
		public CsGlobalNamespace TryParse(out int offset, out int length)
		{
			m_try = true;
			m_bad = new Token();
			CsGlobalNamespace globals = DoParseCompilationUnit();
			
			if (m_bad.Length == 0)
			{
				offset = 0;
				length = 0;
			}
			else
			{
				offset = m_bad.Offset;
				length = m_bad.Length;
			}

			return globals;
		}
		
		#region Private Methods	
		// accessor-declarations:
		//      get-accessor-declaration   set-accessor-declaration?
		//      set-accessor-declaration   get-accessor-declaration?
		// 
		// get-accessor-declaration:
		//     attributes?   accessor-modifier?   get   accessor-body
		// 
		// set-accessor-declaration:
		//     attributes?   accessor-modifier?   set   accessor-body
		// 
		// accessor-body:
		//     block
		//     ;
		private void DoParseAccessorDeclarations(string name, ref bool isGetter, ref bool isSetter, ref CsBody getterBody, ref CsBody setterBody, ref CsAttribute[] getAttrs, ref CsAttribute[] setAttrs, ref MemberModifiers getAccess, ref MemberModifiers setAccess )
		{
			CsAttribute[] attrs = DoParseAttributes();
			MemberModifiers modifiers = DoParseModifiers();

			Token last = m_scanner.Token;
			if (m_scanner.Token.IsIdentifier("get"))
			{
				isGetter = true;
				getAttrs = attrs;
				getAccess = modifiers;
				m_scanner.Advance();
	
				if (m_scanner.Token.IsPunct(";"))
				{
					DoParsePunct(";");
				}
				else
				{
					Token start = m_scanner.Token;
					Token first = m_scanner.Token;
					DoSkipBody("{", "}", ref first, ref last);
					getterBody = new CsBody("get_" + name, start.Offset, first.Offset, last.Offset + last.Length - first.Offset, start.Line);
				}
			}
			else if (m_scanner.Token.IsIdentifier("set"))
			{
				isSetter = true;
				setAttrs = attrs;
				setAccess = modifiers;
				m_scanner.Advance();
	
				if (m_scanner.Token.IsPunct(";"))
				{
					DoParsePunct(";");
				}
				else
				{
					Token first = m_scanner.Token;
					Token start = m_scanner.Token;
					DoSkipBody("{", "}", ref first, ref last);
					setterBody = new CsBody("set_" + name, start.Offset, first.Offset, last.Offset + last.Length - first.Offset, start.Line);
				}
			}
			else
				throw new CsParserException("Expected 'get' or 'set' on line {0}, but found '{1}'", m_scanner.Token.Line, m_scanner.Token.Text());
		}
				
		// attributes:
		//     attribute-sections
		// 
		// attribute-sections:
		//     attribute-section
		//     attribute-sections   attribute-section
		// 
		// attribute-section:
		//      [   attribute-target-specifier?   attribute-list   ]
		//      [   attribute-target-specifier?   attribute-list   ,   ]
		// 
		// attribute-target-specifier:
		//      attribute-target   :
		private CsAttribute[] DoParseAttributes()
		{
			var attrs = new List<CsAttribute>();
			
			while (m_scanner.Token.IsPunct("["))
			{
				Token first = m_scanner.Token;
				m_scanner.Advance();
				
				string target = null;
				if (m_scanner.LookAhead(1).IsPunct(":"))
				{
					Token last = m_scanner.Token;
					target = DoParseIdentifier(ref last);
					m_scanner.Advance();
				}
								
				DoParseAttributeList(first, target, attrs);
				DoParsePunct("]");
			}
			
			return attrs.ToArray();
		}
		
		// attribute-list:
		//      attribute
		//      attribute-list   ,   attribute
		// 
		// attribute:
		//      attribute-name   attribute-arguments?
		// 
		// attribute-name:
		//       type-name
		// 
		// attribute-arguments:
		//      (   positional-argument-list?   )
		//      (   positional-argument-list   ,   named-argument-list   )
		//      (   named-argument-list   )
		private void DoParseAttributeList(Token first, string target, List<CsAttribute> attrs)
		{
			while (true)
			{
				Token last = m_scanner.Token;
				string name = DoParseTypeName(ref last);
				
				string args = string.Empty;
				if (m_scanner.Token.IsPunct("("))
				{	
					args = DoScanBody("(", ")", ref last);
				}
				
				attrs.Add(new CsAttribute(target, name, args, first.Offset, last.Offset + last.Length - first.Offset, first.Line));

				if (m_scanner.Token.IsPunct(","))
					m_scanner.Advance();
				else
					break;
			}
		}
		
		// class-declaration:
		//      attributes?   class-modifiers?   partial?   class   identifier   type-parameter-list?
		//         class-base?   type-parameter-constraints-clauses?   class-body  ;?
		private CsType DoParseClassDeclaration(CsAttribute[] attrs, MemberModifiers modifiers, Token first, MemberModifiers defaultAccess)
		{
			// partial?
			if (m_scanner.Token.IsIdentifier("partial"))
			{
				m_scanner.Advance();
				modifiers |= MemberModifiers.Partial;
			}
			
			// class
			DoParseKeyword("class");

			// identifier
			Token last = m_scanner.Token;
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			
			// type-parameter-list?
			string gargs = null;
			if (m_scanner.Token.IsPunct("<"))
			{
				gargs = DoScanBody("<", ">", ref last);
			}
			
			// class-base?
			CsBases bases = DoParseInterfaceTypeList(last);
			
			// type-parameter-constraints-clauses?   
			string constraints = DoParseTypeParameterConstraintsClauses();
			
			// class-body  
			var members = new List<CsMember>();
			var types = new List<CsType>();
			Token open = m_scanner.Token;
			Token start = m_scanner.Token;
			DoParseStructBody(members, types, ref open, ref last);
			Token close = last;
			
			// ;?
			if (m_scanner.Token.IsPunct(";"))
			{
				last = m_scanner.Token;
				m_scanner.Advance();
			}
			
			CsBody body = new CsBody(name, start.Offset, open.Offset, close.Offset + close.Length - start.Offset, start.Line);
			return new CsClass(nameOffset, body, members.ToArray(), types.ToArray(), bases, constraints, gargs, attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line);
		}
		
		// This is just like struct-member-declaration except it adds destructor-declaration.
		// class-member-declaration:
		//      constant-declaration					attributes?   modifiers?   'const'
		//      field-declaration							attributes?   modifiers?   type         identifier            ','|'='|';'
		//      method-declaration						attributes?   modifiers?   partial?     return-type        member-name  type-parameter-list?    (
		//      property-declaration					attributes?   modifiers?   type         member-name   '{'
		//      event-declaration						attributes?   modifiers?   'event'
		//      indexer-declaration						attributes?   modifiers?   type         'this'
		//      operator-declaration					attributes?   modifiers     type        'operator'
		//                                                                                          'implicit'
		//      constructor-declaration				attributes?   modifiers?   identifier  '('
		//      destructor-declaration					attributes?   'extern'?      '~'
		//      static-constructor-declaration		attributes?   modifiers    identifier   '('
		//      type-declaration
		//           class-declaration					attributes?   modifiers?   partial?    'class'
		//           struct-declaration					attributes?   modifiers?   partial?    'struct'
		//           interface-declaration				attributes?   modifiers?   partial?    'interface'
		//           enum-declaration					attributes?   modifiers?   'enum'
		//           delegate-declaration				attributes?   modifiers?   'delegate'
		private void DoParseClassMemberDeclaration(List<CsMember> members, List<CsType> types)
		{
			while (m_scanner.Token.IsValid() && !m_scanner.Token.IsPunct("}"))
			{
				try
				{
					DoParseClassMemberDeclaration2(members, types);
				}
				catch (Exception e)
				{
					if (m_try)
					{
						if (m_bad.Length == 0)
						{
							Log.WriteLine(TraceLevel.Warning, "Errors", "{0}", e.Message);
							m_bad = m_scanner.Token;
						}
						
						if (m_scanner.Token.IsValid())
							m_scanner.Advance();
					}
					else
						throw;
				}
			}
		}
		
		private void DoParseClassMemberDeclaration2(List<CsMember> members, List<CsType> types)
		{
			// attributes?
			CsAttribute[] attrs = DoParseAttributes();
			
			// modifiers
			Token first = m_scanner.Token;
			MemberModifiers modifiers = DoParseModifiers();
			if (((int) modifiers & CsMember.AccessMask) == 0)
				modifiers = MemberModifiers.Private;
				
				// 'const'
				if (m_scanner.Token.IsIdentifier("const"))
				{
					m_scanner.Advance();
					DoParseConstantDeclaration(members, attrs, modifiers, first);
				}
				// 'event'
				else if (m_scanner.Token.IsIdentifier("event"))
				{
					m_scanner.Advance();
					DoParseEventDeclaration(members, attrs, modifiers, first);
				}
				// 'implicit'
				else if (m_scanner.Token.IsIdentifier("implicit"))
				{
					m_scanner.Advance();
					DoParseConversionOperatorDeclaration(true, members, attrs, modifiers, first);
				}
				// 'explicit'
				else if (m_scanner.Token.IsIdentifier("explicit"))
				{
					m_scanner.Advance();
					DoParseConversionOperatorDeclaration(false, members, attrs, modifiers, first);
				}
				// '~'
				else if (m_scanner.Token.IsPunct("~"))
				{
					m_scanner.Advance();
					DoParseDestructorDeclaration(members, attrs, modifiers, first);
				}
				// class, struct, interface, enum, or delegate
				else if (m_scanner.Token.IsIdentifier("class") || m_scanner.Token.IsIdentifier("struct") || m_scanner.Token.IsIdentifier("interface") || m_scanner.Token.IsIdentifier("enum") || m_scanner.Token.IsIdentifier("delegate"))
				{
					DoParseTypeDeclarationStub(first, attrs, modifiers, members, types, MemberModifiers.Private);
				}
				// partial  class, struct, interface, enum, or delegate
				else if (m_scanner.Token.IsIdentifier("partial") && (m_scanner.LookAhead(1).IsIdentifier("class") || m_scanner.LookAhead(1).IsIdentifier("struct") || m_scanner.LookAhead(1).IsIdentifier("interface") || m_scanner.LookAhead(1).IsIdentifier("enum") || m_scanner.LookAhead(1).IsIdentifier("delegate")))
				{
					m_scanner.Advance();
					DoParseTypeDeclarationStub(first, attrs, modifiers | MemberModifiers.Partial, members, types, MemberModifiers.Private);
				}
				// partial   return-type  member-name  type-parameter-list?    (
				else if (m_scanner.Token.IsIdentifier("partial"))
				{
					m_scanner.Advance();
					DoParseMethodDeclaration(first, attrs, modifiers | MemberModifiers.Partial, members);
				}
				else
				{
					int nameOffset = m_scanner.Token.Offset;
					string typeOrName = DoParseType();
				
					// type 'this'
					if (m_scanner.Token.IsIdentifier("this"))
					{
						nameOffset = m_scanner.Token.Offset;
						m_scanner.Advance();
						DoParseIndexerDeclaration(typeOrName, "<this>", nameOffset, members, attrs, modifiers, first);
					}
					// type 'operator'
					else if (m_scanner.Token.IsIdentifier("operator"))
					{
						m_scanner.Advance();
						DoParseOperatorDeclaration(typeOrName, members, attrs, modifiers, first);
					}
					// identifier  '('
					else if (m_scanner.Token.IsPunct("("))
					{
						m_scanner.Advance();
						DoParseConstructorDeclaration(typeOrName, nameOffset, members, attrs, modifiers, first);
					}
					// type  identifier  ',' | '=' | ';'
					else if (m_scanner.Token.Kind == TokenKind.Identifier && (m_scanner.LookAhead(1).IsPunct(",") || m_scanner.LookAhead(1).IsPunct("=") || m_scanner.LookAhead(1).IsPunct(";")))
					{
						DoParseFieldDeclaration(typeOrName, members, attrs, modifiers, first);
					}
					else
					{
						Token last = m_scanner.Token;	
						nameOffset = m_scanner.Token.Offset;
						string name = DoParseNamespaceOrTypeName(ref last);
						
						// type   interface-type   '.'   'this'
						if (name.EndsWith(".this"))
						{
							DoParseIndexerDeclaration(typeOrName, name, nameOffset, members, attrs, modifiers, first);
						}
						// type   member-name   '{'
						else if (m_scanner.Token.IsPunct("{"))
						{
							DoParsePropertyDeclaration(typeOrName, name, nameOffset, members, attrs, modifiers, first);
						}
						// type   member-name   '<' | '('
						else if (m_scanner.Token.IsPunct("<") || m_scanner.Token.IsPunct("("))
						{
							DoParseMethodStub(typeOrName, name, nameOffset, first, attrs, modifiers, members);
						}
						else
							throw new CsParserException("Expected member declaration on line {0}, but found '{1}'", m_scanner.Token.Line, m_scanner.Token.Text());
					}
			}
		}
		
		// constructor-declaration:
		//     attributes?   constructor-modifiers?   constructor-declarator   constructor-body
		// 
		// constructor-declarator:
		//     identifier   (   formal-parameter-list?   )   constructor-initializer?
		// 
		// constructor-initializer:
		//     :   base   (   argument-list?   )
		//     :   this   (   argument-list?   )
		// 
		// constructor-body:
		//     block
		//     ;
		private void DoParseConstructorDeclaration(string name, int nameOffset, List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{			
			var parms = new List<CsParameter>();
			DoParseFormalParameterList(parms);
			DoParsePunct(")");
			
			Token last = m_scanner.Token;
			Token open = last;
			if (m_scanner.Token.IsPunct(":"))
			{
				m_scanner.Advance();
				DoParseIdentifier(ref last);
				DoSkipBody("(", ")", ref open, ref last);
			}
			
			last = m_scanner.Token;
			open = new Token();
			Token start = m_scanner.Token;
			Token close = last;
			if (m_scanner.Token.IsPunct(";"))
			{
				m_scanner.Advance();
			}
			else
			{
				DoSkipBody("{", "}", ref open, ref last);
				close = last;
			}

			CsBody body = open.Length > 0 ? new CsBody(name, start.Offset, open.Offset, close.Offset + close.Length - start.Offset, start.Line) : null;
			members.Add(new CsMethod(nameOffset, body, true, false, null, parms.ToArray(), null, "void", attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// operator-declaration:
		//     attributes?   operator-modifiers   operator-declarator   operator-body
		// 
		// conversion-operator-declarator:
		//     implicit   operator   type   (   type   identifier   )
		//     explicit   operator   type   (   type   identifier   )
		private void DoParseConversionOperatorDeclaration(bool isImplicit, List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			DoParseKeyword("operator");
			int nameOffset = m_scanner.Token.Offset;
			string type = DoParseType();
			
			var parms = new List<CsParameter>();
			DoParsePunct("(");
			DoParseFormalParameterList(parms);
			DoParsePunct(")");
			
			Token last = m_scanner.Token;
			CsBody body = null;
			if (m_scanner.Token.IsPunct(";"))
			{
				m_scanner.Advance();
			}
			else
			{
				Token f = m_scanner.Token;
				Token start = m_scanner.Token;
				DoSkipBody("{", "}", ref f, ref last);
				body = new CsBody(isImplicit ? "op_Implict" : "op_Explict", start.Offset, f.Offset, last.Offset + last.Length - f.Offset, start.Line);
			}
			
			members.Add(new CsOperator(nameOffset, body, isImplicit, !isImplicit, parms.ToArray(), type, attrs, modifiers, "<conversion>", first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// compilation-unit:
		//     extern-alias-directives?   using-directives?  global-attributes?  namespace-member-declarations?
		private CsGlobalNamespace DoParseCompilationUnit()
		{
			Token last = m_scanner.Token;
			
			var externs = new List<CsExternAlias>();
			var attrs = new List<CsAttribute>();
			var namespaces = new List<CsNamespace>();
			var aliases = new List<CsUsingAlias>();
			var uses = new List<CsUsingDirective>();
			var members = new List<CsMember>();
			var types = new List<CsType>();
			
			try
			{
				DoParseExternAliasDirectives(ref last, externs);
				DoParseUsingDirectives(ref last, aliases, uses);
				DoParseGlobalAttributes(ref last, attrs);
				DoParseNamespaceMemberDeclarations(ref last, namespaces, members, types);
				
				if (m_scanner.Token.Kind != TokenKind.Invalid)
					throw new CsParserException("Expected eof on line {0}, but found '{1}'", m_scanner.Token.Line, m_scanner.Token.Text());
			}
			catch (Exception e)
			{
				if (m_try)
				{
					if (m_bad.Length == 0)
					{
						Log.WriteLine(TraceLevel.Warning, "Errors", "{0}", e.Message);
						m_bad = m_scanner.Token;
					}
				}
				else
					throw;
			}
			
			return new CsGlobalNamespace(m_scanner.Preprocess, new CsBody("<globals>", m_text.Length), attrs.ToArray(), externs.ToArray(), aliases.ToArray(), uses.ToArray(), namespaces.ToArray(), members.ToArray(), types.ToArray(), last.Offset + last.Length);
		}
			
		// constant-declaration:
		//     attributes?   constant-modifiers?   const   type   constant-declarators   ;
		// 
		// constant-declarators:
		//     constant-declarator
		//     constant-declarators   ,   constant-declarator
		private void DoParseConstantDeclaration(List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			string type = DoParseType();
			modifiers |= MemberModifiers.Const;
			
			while (true)
			{
				DoParseFieldDeclarator(type, members, attrs, modifiers, first);
				
				if (m_scanner.Token.IsPunct(","))
					m_scanner.Advance();
				else
					break;
			}
			
			DoParsePunct(";");
		}
		
		// delegate-declaration:
		//     attributes?   delegate-modifiers?   delegate  return-type   identifier
		//          type-parameter-list?
		//              (   formal-parameter-list?   )   type-parameter-constraints-clauses?   ;
		// 
		// type-parameter-list:
		//     <   type-parameters   >
		private CsMember DoParseDelegateDeclaration(CsAttribute[] attrs, MemberModifiers modifiers, Token first, MemberModifiers defaultAccess)
		{
			Token last = m_scanner.Token;
			string rtype = DoParseReturnType();
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			
			string gargs = null;
			if (m_scanner.Token.IsPunct("<"))
			{
				gargs = DoScanBody("<", ">", ref last);
			}
			
			var parms = new List<CsParameter>();
			DoParsePunct("(");
			DoParseFormalParameterList(parms);
			DoParsePunct(")");
			string constraints = DoParseTypeParameterConstraintsClauses();
			last = DoParsePunct(";");
			
			if (((int) modifiers & CsMember.AccessMask) == 0)
				modifiers |= defaultAccess;
			
			return new CsDelegate(nameOffset, constraints, parms.ToArray(), gargs, rtype, attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line);
		}
		
		// destructor-declaration:
		//     attributes?  extern?   ~   identifier   (   )    destructor-body
		// 
		// destructor-body:
		//     block
		//     ;
		private void DoParseDestructorDeclaration(List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			Token last = first;
			int nameOffset = m_scanner.Token.Offset;
			string name = "~" + DoParseIdentifier(ref last);
			DoParsePunct("(");
			DoParsePunct(")");
			
			last = m_scanner.Token;
			Token start = m_scanner.Token;
			Token open = new Token();
			Token close = last;
			if (m_scanner.Token.IsPunct(";"))
			{
				m_scanner.Advance();
			}
			else
			{
				DoSkipBody("{", "}", ref open, ref last);
				close = last;
			}
			
			CsBody body = open.Length > 0 ? new CsBody(name, start.Offset, open.Offset, close.Offset + close.Length - start.Offset, start.Line) : null;
			members.Add(new CsMethod(nameOffset, body, false, true, null, new CsParameter[0], null, "void", attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// enum-declaration:
		//     attributes?   enum-modifiers?   enum   identifier   enum-base?   enum-body   ;?
		// 
		// enum-base:
		//    :   integral-type
		// 
		// enum-body:
		//    {   enum-member-declarations?   }
		//    {   enum-member-declarations   ,   }
		private CsMember DoParseEnumDeclaration(CsAttribute[] attrs, MemberModifiers modifiers, Token first, MemberModifiers defaultAccess)
		{
			Token last = first;
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			
			string baseType = "int";
			if (m_scanner.Token.IsPunct(":"))
			{
				m_scanner.Advance();
				baseType = DoParseIdentifier(ref last);
			}
			
			Token open = m_scanner.Token;
			DoSkipBody("{", "}", ref open, ref last);

			if (((int) modifiers & CsMember.AccessMask) == 0)
				modifiers |= defaultAccess;
			
			return new CsEnum(nameOffset, baseType, attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line);
		}

		// event-declaration:
		//     attributes?   event-modifiers?   event   type   variable-declarators   ;
		//     attributes?   event-modifiers?   event   type   member-name   {   event-accessor-declarations   }
		// 
		// variable-declarators:
		//     variable-declarator
		//     variable-declarators   ,   variable-declarator
		private void DoParseEventDeclaration(List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			string type = DoParseType();
			
			Token next = m_scanner.LookAhead(1);
			if (next.IsPunct("=") || next.IsPunct(",") || next.IsPunct(";"))
			{
				while (true)
				{
					DoParseEventDeclarator(type, members, attrs, modifiers, first);
					
					if (m_scanner.Token.IsPunct(","))
						m_scanner.Advance();
					else
						break;
				}
				
				DoParsePunct(";");
			}
			else
			{
				int nameOffset = m_scanner.Token.Offset;
				string name = DoParseMemberName();
				
				Token last = m_scanner.Token;
				Token open = m_scanner.Token;
				DoSkipBody("{", "}", ref open, ref last);
				
				members.Add(new CsEvent(nameOffset, type, name, attrs, modifiers, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
			}
		}
		
		// variable-declarator:
		//     identifier
		//     identifier   =   variable-initializer
		private void DoParseEventDeclarator(string type, List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			Token last = m_scanner.Token;
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			
			if (m_scanner.Token.IsPunct("="))
			{
				DoParsePunct("=");
				
				// TODO: this won't parse multiple declarators correctly. We could probably handle this
				// by scanning until we hit a semi-colon or a comma not within brackets.
				while (m_scanner.Token.IsValid() && !m_scanner.Token.IsPunct(";"))
				{
					last = m_scanner.Token;
					m_scanner.Advance();
				}
			}
						
			members.Add(new CsEvent(nameOffset, type, name, attrs, modifiers, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// TODO: this won't parse multiple declarators correctly. One possible fix is
		// to scan tokens until we hit a semi-colon or until we hit a comma not
		// inside brackets. Another is to add a simple parser which ignores things 
		// like precedence. If this is fixed also update DoParseEventDeclarator.
		private string DoParseExpression(ref Token last)
		{
			Token start = m_scanner.Token;
			while (m_scanner.Token.IsValid() && !m_scanner.Token.IsPunct(";"))	
			{
				m_scanner.Advance();
			}
			last = m_scanner.Token;
			
			return m_text.Substring(start.Offset, last.Offset - start.Offset);
		}
		
		// extern-alias-directives:
		//     extern-alias-directive
		//     extern-alias-directives   extern-alias-directive
		// 
		// extern-alias-directive:
		//     extern   alias   identifier   ;
		private void DoParseExternAliasDirectives(ref Token last, List<CsExternAlias> externs)
		{
			while (m_scanner.Token.IsIdentifier("extern"))
			{
				Token first = m_scanner.Token;
				m_scanner.Advance();
				
				DoParseKeyword("alias");
				
				string name = DoParseIdentifier(ref last);
				last = DoParsePunct(";");
				externs.Add(new CsExternAlias(name, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
			}
		}
		
		// field-declaration:
		//      attributes?   field-modifiers?   type   variable-declarators  ;
		private void DoParseFieldDeclaration(string type, List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{			
			while (true)
			{
				DoParseFieldDeclarator(type, members, attrs, modifiers, first);

				if (m_scanner.Token.IsPunct(","))
					m_scanner.Advance();
				else
					break;
			}
			
			DoParsePunct(";");
		}
		
		// constant-declarator:
		//     identifier   =   constant-expression
		// 
		// field-declaration:
		//      attributes?   field-modifiers?   type   variable-declarators  ;
		// 
		// variable-declarator:
		//     identifier
		//     identifier   =   variable-initializer
		private void DoParseFieldDeclarator(string type, List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			Token last = m_scanner.Token;
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			
			string value = null;
			if (m_scanner.Token.IsPunct("="))
			{
				DoParsePunct("=");								
				value = DoParseExpression(ref last);
			}
			
			members.Add(new CsField(nameOffset, type, value, attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// formal-parameter-list:
		//     fixed-parameters
		//     fixed-parameters   ,   parameter-array
		//     parameter-array
		// 
		// fixed-parameters:
		//      fixed-parameter
		//      fixed-parameters   ,   fixed-parameter
		// 
		// fixed-parameter:
		//      attributes?   parameter-modifier?   type   identifier
		// 
		// parameter-array:
		//     attributes?   params   array-type   identifier
		private void DoParseFormalParameterList(List<CsParameter> parms)
		{
			while (m_scanner.Token.IsValid() && !m_scanner.Token.IsPunct(")") && !m_scanner.Token.IsPunct("]"))	// ] is for indexers
			{
				CsAttribute[] attrs = DoParseAttributes();
				ParameterModifier modifier = DoParseParameterModifier();
				
				bool isParams = false;
				if (m_scanner.Token.IsIdentifier("params"))
				{
					m_scanner.Advance();
					isParams = true;
				}

				Token last = m_scanner.Token;
				string type = DoParseType();
				string name = DoParseIdentifier(ref last);
				
				if (m_scanner.Token.IsPunct(","))
					m_scanner.Advance();
				
				parms.Add(new CsParameter(attrs, modifier, isParams, type, name));
			}
		}
		
		// global-attributes:
		//     global-attribute-sections
		// 
		// global-attribute-sections:
		//     global-attribute-section
		//     global-attribute-sections   global-attribute-section
		// 
		// global-attribute-section:
		//     [   global-attribute-target-specifier   attribute-list   ]
		//     [   global-attribute-target-specifier   attribute-list   ,   ]
		// 
		// global-attribute-target-specifier:
		//     global-attribute-target  :
		// 
		// global-attribute-target:
		//     assembly
		//     module
		private void DoParseGlobalAttributes(ref Token last, List<CsAttribute> attrs)
		{
			while (m_scanner.Token.IsPunct("["))
			{
				// This is a bit tricky: we can't tell if the attribute is a global attribute until
				// we get the target (if any).
				Token l1 = m_scanner.LookAhead(1);	// target or attribute name
				Token l2 = m_scanner.LookAhead(2);	// : or (
				
				if ((l1 == "assembly" || l1 == "module") && l2 == ":")
				{
					Token first = m_scanner.Token;
					m_scanner.Advance();
					
					string target = DoParseIdentifier(ref last);
					
					DoParsePunct(":");
					DoParseAttributeList(first, target, attrs);
					last = DoParsePunct("]");
				}
				else
					break;		// we'll treat it as a type attribute
			}
		}
						
		private string DoParseIdentifier(ref Token last)
		{
			if (m_scanner.Token.Kind != TokenKind.Identifier)
				throw new CsParserException("Expected an identifier on line {0}, but found '{1}'", m_scanner.Token.Line, m_scanner.Token.Text());
			
			last = m_scanner.Token;
			string name = last.Text();
			m_scanner.Advance();
			
			return name;
		}
		
		// indexer-declaration:
		//     attributes?   indexer-modifiers?   indexer-declarator   {   accessor-declarations   }
		// 
		// indexer-declarator:
		//     type   this   [   formal-parameter-list   ]
		//     type   interface-type   .   this   [   formal-parameter-list   ]
		private void DoParseIndexerDeclaration(string type, string name, int nameOffset, List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			var parms = new List<CsParameter>();
			DoParsePunct("[");
			DoParseFormalParameterList(parms);
			DoParsePunct("]");

			bool hasGet = false, hasSet = false;
			CsBody getterBody = null, setterBody = null;
			CsAttribute[] getAttrs = null, setAttrs = null;
			MemberModifiers getAccess = 0, setAccess = 0;
			DoParsePunct("{");
			DoParseAccessorDeclarations(name, ref hasGet, ref hasSet, ref getterBody, ref setterBody, ref getAttrs, ref setAttrs, ref getAccess, ref setAccess);
			if (!m_scanner.Token.IsPunct("}"))
				DoParseAccessorDeclarations(name, ref hasGet, ref hasSet, ref getterBody, ref setterBody, ref getAttrs, ref setAttrs, ref getAccess, ref setAccess);
			Token last = DoParsePunct("}");
		
			members.Add(new CsIndexer(nameOffset, getterBody, setterBody, getAccess, setAccess, name, getAttrs, setAttrs, hasGet, hasSet, parms.ToArray(), type, attrs, modifiers, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// interface-accessors:
		//      attributes?   get   ;
		//      attributes?   set   ;
		//      attributes?   get   ;   attributes?   set   ;
		//      attributes?   set   ;   attributes?   get   ;
		private void DoParseInterfaceAccessors(ref bool isGetter, ref bool isSetter, ref CsAttribute[] getAttrs, ref CsAttribute[] setAttrs)
		{
			CsAttribute[] attrs = DoParseAttributes();

			if (m_scanner.Token.IsIdentifier("get"))
			{
				isGetter = true;
				getAttrs = attrs;
			}
			else if (m_scanner.Token.IsIdentifier("set"))
			{
				isSetter = true;
				setAttrs = attrs;
			}
			else
				throw new CsParserException("Expected 'get' or 'set' on line {0}, but found '{1}'", m_scanner.Token.Line, m_scanner.Token.Text());
			m_scanner.Advance();

			DoParsePunct(";");
		}
				
		// interface-body:
		//      {   interface-member-declarations?   }
		// 
		// interface-member-declarations:
		//      interface-member-declaration
		//      interface-member-declarations   interface-member-declaration
		// 
		// interface-member-declaration:
		//      interface-method-declaration		attributes?   new?   return-type   identifier   type-parameter-list  (
		//      interface-property-declaration		attributes?   new?   type             identifier   {
		//      interface-event-declaration			attributes?   new?   event
		//      interface-indexer-declaration		attributes?   new?   type             this
		private void DoParseInterfaceBody(List<CsMember> members, List<CsType> types, ref Token open, ref Token last)
		{
			last = DoParsePunct("{");
			open = m_scanner.Token;
			
			while (m_scanner.Token.IsValid() && !m_scanner.Token.IsPunct("}"))
			{
				try
				{
					DoParseInterfaceBody2(members,types, ref open, ref last);
				}
				catch (Exception e)
				{
					if (m_try)
					{
						if (m_bad.Length == 0)
						{
							Log.WriteLine(TraceLevel.Warning, "Errors", "{0}", e.Message);
							m_bad = m_scanner.Token;
						}
						
						if (m_scanner.Token.IsValid())
							m_scanner.Advance();
					}
					else
						throw;
				}
			}
			
			if (!m_try || m_scanner.Token.IsPunct("}"))
				last = DoParsePunct("}");
			else
				last = m_scanner.Token;
		}
		
		private void DoParseInterfaceBody2(List<CsMember> members, List<CsType> types, ref Token open, ref Token last)
		{
			CsAttribute[] attrs = DoParseAttributes();
			
			Token first = m_scanner.Token;
			MemberModifiers modifiers = MemberModifiers.Public;
			if (m_scanner.Token.IsIdentifier("new"))
			{
				m_scanner.Advance();
				modifiers |= MemberModifiers.New;
			}
			
			if (m_scanner.Token.IsIdentifier("event"))
			{
				m_scanner.Advance();
				DoParseInterfaceEventDeclaration(members, attrs, modifiers, first);
			}
			else
			{
				string rtype = DoParseType();
				if (m_scanner.Token.IsIdentifier("this"))
				{
					int nameOffset = m_scanner.Token.Offset;
					m_scanner.Advance();
					DoParseInterfaceIndexerDeclaration(nameOffset, members, attrs, modifiers, rtype, first);
				}
				else if (m_scanner.LookAhead(1).IsPunct("{"))
				{
					DoParseInterfacePropertyDeclaration(members, attrs, modifiers, rtype, first);
				}
				else
					DoParseInterfaceMethodDeclaration(members, attrs, modifiers, rtype, first);
			}
		}
		
		// interface-declaration:
		//     attributes?   interface-modifiers?   partial?   interface   identifier   type-parameter-list?
		//              interface-base?   type-parameter-constraints-clauses?   interface-body  ;?
		private CsType DoParseInterfaceDeclaration(CsAttribute[] attrs, MemberModifiers modifiers, Token first, MemberModifiers defaultAccess)
		{
			// partial?
			if (m_scanner.Token.IsIdentifier("partial"))
			{
				m_scanner.Advance();
				modifiers |= MemberModifiers.Partial;
			}
			
			// interface
			DoParseKeyword("interface");
			
			// identifier
			Token last = m_scanner.Token;
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			
			// type-parameter-list?
			string gargs = null;
			if (m_scanner.Token.IsPunct("<"))
			{
				gargs = DoScanBody("<", ">", ref last);
			}
			
			// interface-base?
			CsBases bases = DoParseInterfaceTypeList(last);
			
			// type-parameter-constraints-clauses?   
			string constraints = DoParseTypeParameterConstraintsClauses();
			
			// interface-body  
			var members = new List<CsMember>();
			var types = new List<CsType>();
			Token open = m_scanner.Token;
			Token start = m_scanner.Token;
			DoParseInterfaceBody(members, types, ref open, ref last);
			Token close = last;
			
			// ;?
			if (m_scanner.Token.IsPunct(";"))
			{
				last = m_scanner.Token;
				m_scanner.Advance();
			}
				
			CsBody body = new CsBody(name, start.Offset, open.Offset, close.Offset + close.Length - start.Offset, start.Line);
			return new CsInterface(nameOffset, body, members.ToArray(), types.ToArray(), bases, constraints, gargs, attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line);
		}
		
		// interface-event-declaration:
		//      attributes?   new?   event   type   identifier   ;
		private void DoParseInterfaceEventDeclaration(List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			Token last = m_scanner.Token;
			
			string type = DoParseType();
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			last = DoParsePunct(";");
			
			members.Add(new CsEvent(nameOffset, type, name, attrs, modifiers, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// interface-indexer-declaration:
		//      attributes?   new?   type   this   [   formal-parameter-list   ]   {   interface-accessors   }
		private void DoParseInterfaceIndexerDeclaration(int nameOffset, List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, string rtype, Token first)
		{
			var parms = new List<CsParameter>();
			DoParsePunct("[");
			DoParseFormalParameterList(parms);
			DoParsePunct("]");

			bool hasGet = false, hasSet = false;
			CsAttribute[] getAttrs = null;
			CsAttribute[] setAttrs = null;
			DoParsePunct("{");
			DoParseInterfaceAccessors(ref hasGet, ref hasSet, ref getAttrs, ref setAttrs);
			if (!m_scanner.Token.IsPunct("}"))
				DoParseInterfaceAccessors(ref hasGet, ref hasSet, ref getAttrs, ref setAttrs);
			Token last = DoParsePunct("}");
			
			members.Add(new CsIndexer(nameOffset, null, null, 0, 0, "<this>", getAttrs, setAttrs, hasGet, hasSet, parms.ToArray(), rtype, attrs, modifiers, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// interface-method-declaration:
		//      attributes?   new?   return-type   identifier   type-parameter-list
		//         (   formal-parameter-list?   )   type-parameter-constraints-clauses?   ;
		private void DoParseInterfaceMethodDeclaration(List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, string rtype, Token first)
		{
			Token last = m_scanner.Token;
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			
			string gargs = null;
			if (m_scanner.Token.IsPunct("<"))
			{
				gargs = DoScanBody("<", ">", ref last);
			}
			
			var parms = new List<CsParameter>();
			DoParsePunct("(");
			DoParseFormalParameterList(parms);
			DoParsePunct(")");
			
			string constraints = DoParseTypeParameterConstraintsClauses();
			last = DoParsePunct(";");
		
			members.Add(new CsMethod(nameOffset, null, false, false, constraints, parms.ToArray(), gargs, rtype, attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// interface-property-declaration:
		//      attributes?   new?   type   identifier   {   interface-accessors   }
		private void DoParseInterfacePropertyDeclaration(List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, string rtype, Token first)
		{
			Token last = m_scanner.Token;
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			
			bool hasGet = false, hasSet = false;
			CsAttribute[] getAttrs = null;
			CsAttribute[] setAttrs = null;
			DoParsePunct("{");
			DoParseInterfaceAccessors(ref hasGet, ref hasSet, ref getAttrs, ref setAttrs);
			if (m_scanner.Token.Kind == TokenKind.Identifier)
				DoParseInterfaceAccessors(ref hasGet, ref hasSet, ref getAttrs, ref setAttrs);
			last = DoParsePunct("}");
		
			members.Add(new CsProperty(nameOffset, null, null, 0, 0, name, getAttrs, setAttrs, hasGet, hasSet, rtype, attrs, modifiers, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// interface-type-list:
		//      interface-type
		//      interface-type-list   ,   interface-type
		private CsBases DoParseInterfaceTypeList(Token previous)
		{
			var interfaces = new List<string>();
			
			Token first = previous;
			Token last = previous;
			if (m_scanner.Token.IsPunct(":"))
			{
				m_scanner.Advance();
				
				first = m_scanner.Token;
				while (true)
				{
					string name = DoParseTypeName(ref last).TrimAll();
					interfaces.Add(name);
						
					if (m_scanner.Token.IsPunct(","))
						m_scanner.Advance();
					else
						break;
				}
			}
			
			if (interfaces.Count > 0)
				return new CsBases(interfaces.ToArray(), first.Offset, last.Offset + last.Length - first.Offset, first.Line);
			else
				return new CsBases(previous.Offset + previous.Length, previous.Line);
		}
		
		private Token DoParseKeyword(string name)
		{
			if (!m_scanner.Token.IsIdentifier(name))
				throw new CsParserException("Expected '{0}' on line {1}, but found '{2}'", name, m_scanner.Token.Line, m_scanner.Token.Text());
			
			Token token = m_scanner.Token;
			m_scanner.Advance();
			
			return token;
		}
	
		// member-name:
		//    identifier
		//    interface-type   .   identifier
		private string DoParseMemberName()
		{
			Token last = m_scanner.Token;
			string name = DoParseNamespaceOrTypeName(ref last);
			
			if (m_scanner.Token.IsPunct("."))
			{
				m_scanner.Advance();
				name += "." + DoParseIdentifier(ref last);
			}
			
			return name;
		}
		
		// method-declaration:
		//     method-header   method-body
		// 
		// method-header:
		//     attributes?   method-modifiers?   partial?   return-type   member-name   type-parameter-list?  (   formal-parameter-list?   )   type-parameter-constraints-clauses?
		private void DoParseMethodDeclaration(Token first, CsAttribute[] attrs, MemberModifiers modifiers, List<CsMember> members)
		{
			string rtype = DoParseReturnType();
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseMemberName();
			
			DoParseMethodStub(rtype, name, nameOffset, first, attrs, modifiers, members);
		}
		
		// method-header:
		//     ...   type-parameter-list?  (   formal-parameter-list?   )   type-parameter-constraints-clauses?
		//
		// method-body:
		//     block
		//     ;
		private void DoParseMethodStub(string rtype, string memberName, int nameOffset, Token first, CsAttribute[] attrs, MemberModifiers modifiers, List<CsMember> members)
		{
			Token last = m_scanner.Token;
			
			string gargs = null;
			if (m_scanner.Token.IsPunct("<"))
			{
				gargs = DoScanBody("<", ">", ref last);
			}
			
			var parms = new List<CsParameter>();
			DoParsePunct("(");
			DoParseFormalParameterList(parms);
			DoParsePunct(")");
			string constraints = DoParseTypeParameterConstraintsClauses();
			
			Token open = new Token();
			Token start = m_scanner.Token;
			Token close = m_scanner.Token;
			if (m_scanner.Token.IsPunct(";"))
			{
				m_scanner.Advance();
			}
			else
			{
				DoSkipBody("{", "}", ref open, ref last);
				close = last;
			}
			
			CsBody body = open.Length > 0 ? new CsBody(memberName, start.Offset, open.Offset, close.Offset + close.Length - start.Offset, start.Line) : null;
			members.Add(new CsMethod(nameOffset, body, false, false, constraints, parms.ToArray(), gargs, rtype, attrs, modifiers, memberName, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// enum-modifiers, delegate-modifiers, class-modifiers, struct-modifiers, interface-modifiers
		private MemberModifiers DoParseModifiers()
		{
			MemberModifiers modifiers = 0;
			
			while (m_scanner.Token.Kind == TokenKind.Identifier)
			{
				if (m_scanner.Token == "abstract")
				{
					modifiers |= MemberModifiers.Abstract;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "const")
				{
					modifiers |= MemberModifiers.Const;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "extern")
				{
					modifiers |= MemberModifiers.Extern;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "internal")
				{
					modifiers |= MemberModifiers.Internal;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "new")
				{
					modifiers |= MemberModifiers.New;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "override")
				{
					modifiers |= MemberModifiers.Override;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "partial")
				{
					modifiers |= MemberModifiers.Partial;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "private")
				{
					modifiers |= MemberModifiers.Private;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "protected")
				{
					modifiers |= MemberModifiers.Protected;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "public")
				{
					modifiers |= MemberModifiers.Public;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "readonly")
				{
					modifiers |= MemberModifiers.Readonly;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "sealed")
				{
					modifiers |= MemberModifiers.Sealed;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "static")
				{
					modifiers |= MemberModifiers.Static;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "unsafe")
				{
					modifiers |= MemberModifiers.Unsafe;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "virtual")
				{
					modifiers |= MemberModifiers.Virtual;
					m_scanner.Advance();
				}
				else if (m_scanner.Token == "volatile")
				{
					modifiers |= MemberModifiers.Volatile;
					m_scanner.Advance();
				}
				else
					break;
			}
			
			return modifiers;
		}
		
		// namespace-body:
		//     {   extern-alias-directives?   using-directives?   namespace-member-declarations?   }
		private void DoParseNamespaceBody(List<CsExternAlias> externs, List<CsUsingAlias> aliases, List<CsUsingDirective> uses, List<CsNamespace> namespaces, List<CsMember> members, List<CsType> types, ref Token first, ref Token last)
		{
			DoParsePunct("{");
			first = m_scanner.Token;
			
			DoParseExternAliasDirectives(ref last, externs);
			DoParseUsingDirectives(ref last, aliases, uses);
			DoParseNamespaceMemberDeclarations(ref last, namespaces, members, types);

			if (!m_try || m_scanner.Token.IsPunct("}"))
				last = DoParsePunct("}");
			else
				last = m_scanner.Token;
		}
		
		// namespace-declaration:
		//     namespace   qualified-identifier   namespace-body   ;?
		private void DoParseNamespaceDeclaration(ref Token last,  List<CsNamespace> namespaces)
		{
			Token first = m_scanner.Token;
			DoParseKeyword("namespace");
			
			string name = DoParseQualifiedIdentifier();
			
			var externs = new List<CsExternAlias>();
			var aliases = new List<CsUsingAlias>();
			var uses = new List<CsUsingDirective>();
			var childNamespaces = new List<CsNamespace>();
			var members = new List<CsMember>();
			var types = new List<CsType>();
			Token open = m_scanner.Token;
			Token start = m_scanner.Token;
			DoParseNamespaceBody(externs, aliases, uses, childNamespaces, members, types, ref open, ref last);
			Token close = last;
			
			if (m_scanner.Token.IsIdentifier(";"))
			{
				last = m_scanner.Token;
				m_scanner.Advance();
			}
			
			CsBody body = new CsBody(name, start.Offset, open.Offset, close.Offset + close.Length - start.Offset, start.Line);
			namespaces.Add(new CsNamespace(body, name, externs.ToArray(), aliases.ToArray(), uses.ToArray(), childNamespaces.ToArray(), members.ToArray(), types.ToArray(), first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// namespace-member-declarations:
		//     namespace-member-declaration
		//     namespace-member-declarations   namespace-member-declaration
		// 
		// namespace-member-declaration:
		//    namespace-declaration
		//    type-declaration
		private void DoParseNamespaceMemberDeclarations(ref Token last,  List<CsNamespace> namespaces, List<CsMember> members, List<CsType> types)
		{
			while (m_scanner.Token.IsValid() && !m_scanner.Token.IsPunct("}"))
			{
				if (m_scanner.Token.IsIdentifier("namespace"))
				{
					DoParseNamespaceDeclaration(ref last, namespaces);
				}
				else
				{
					DoParseTypeDeclaration(members, types, MemberModifiers.Internal);
				}
			}
		}
		
		// namespace-name:
		//     namespace-or-type-name
		private string DoParseNamespaceName()
		{
			Token last = m_scanner.Token;
			return DoParseNamespaceOrTypeName(ref last);
		}
		
		// namespace-or-type-name:
		//    identifier   type-argument-list?
		//    namespace-or-type-name   .   identifier   type-argument-list?
		//    qualified-alias-member
		// 
		// type-argument-list:
		//      <   type-arguments   >
		// 
		// qualified-alias-member:
		//      identifier   ::   identifier   type-argument-list?
		private string DoParseNamespaceOrTypeName(ref Token last)
		{
			string name = DoParseIdentifier(ref last);

			while (m_scanner.Token.IsPunct("."))
			{
				m_scanner.Advance();
				name += "." + DoParseIdentifier(ref last);
			}
			
			if (m_scanner.Token.IsPunct(":") && m_scanner.LookAhead(1).IsPunct(":"))
			{
				m_scanner.Advance();
				m_scanner.Advance();
				name += "::" + DoParseIdentifier(ref last);
			}
			
			if (m_scanner.Token.IsPunct("<"))
			{
				name += "<" + DoScanBody("<", ">", ref last) + ">";
			}
			
			return name;
		}
		
		// operator-declaration:
		//     attributes?   operator-modifiers   operator-declarator   operator-body
		// 
		// operator-declarator:
		//     unary-operator-declarator
		//     binary-operator-declarator
		//     conversion-operator-declarator
		// 
		// unary-operator-declarator:
		//     type   operator   overloadable-unary-operator   (   type   identifier   )
		// 
		// binary-operator-declarator:
		//     type   operator   overloadable-binary-operator   (   type   identifier   ,   type   identifier   )
		// 
		// operator-body:
		//     block
		//     ;
		private void DoParseOperatorDeclaration(string type, List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseOperatorName();
			
			var parms = new List<CsParameter>();
			DoParsePunct("(");
			DoParseFormalParameterList(parms);
			DoParsePunct(")");
			
			Token last = m_scanner.Token;
			CsBody body = null;
			if (m_scanner.Token.IsPunct(";"))
			{
				m_scanner.Advance();
			}
			else
			{
				Token f = m_scanner.Token;
				Token start = m_scanner.Token;
				DoSkipBody("{", "}", ref f, ref last);
				body = new CsBody(name, start.Offset, f.Offset, last.Offset + last.Length - f.Offset, start.Line);
			}
			
			members.Add(new CsOperator(nameOffset, body, false, false, parms.ToArray(), type, attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		// overloadable-unary-operator: one of
		//     +   -   !   ~   ++   --   true   false
		// 
		// overloadable-binary-operator:
		//     +
		//     -
		//     *
		//     /
		//     %
		//     &
		//     |
		//     ^
		//     <<
		//     >>
		//     ==
		//     !=
		//     >
		//     <
		//     >=
		//     <=
		private string DoParseOperatorName()
		{
			string name;
			
			if (m_scanner.Token.IsIdentifier("true"))
			{
				m_scanner.Advance();
				name = "true";
			}
			else if (m_scanner.Token.IsIdentifier("false"))
			{
				m_scanner.Advance();
				name = "false";
			}
			else if (m_scanner.Token.Kind == TokenKind.Other)
			{
				name = m_scanner.Token.Text();
				m_scanner.Advance();
				if (m_scanner.Token.Kind == TokenKind.Other && !m_scanner.Token.IsPunct("("))
				{
					name += m_scanner.Token.Text();
					m_scanner.Advance();
				}
			}
			else
				throw new CsParserException("Expected a unary or binary operator name on line {0}, but found '{1}'", m_scanner.Token.Line, m_scanner.Token.Text());
			
			return name;
		}
		
		// parameter-modifier:
		//     ref
		//     out
		//     this
		private ParameterModifier DoParseParameterModifier()
		{
			ParameterModifier modifier = ParameterModifier.None;
			
			if (m_scanner.Token.IsIdentifier("ref"))
			{
				modifier = ParameterModifier.Ref;
				m_scanner.Advance();
			}
			else if (m_scanner.Token.IsIdentifier("out"))
			{
				modifier = ParameterModifier.Out;
				m_scanner.Advance();
			}
			else if (m_scanner.Token.IsIdentifier("this"))
			{
				modifier = ParameterModifier.This;
				m_scanner.Advance();
			}
			
			return modifier;
		}
		
		// property-declaration:
		//     attributes?   property-modifiers?   type   member-name   {   accessor-declarations   }
		private void DoParsePropertyDeclaration(string type, string name, int nameOffset, List<CsMember> members, CsAttribute[] attrs, MemberModifiers modifiers, Token first)
		{
			bool hasGet = false, hasSet = false;
			CsBody getterBody = null, setterBody = null;
			CsAttribute[] getAttrs = null, setAttrs = null;
			MemberModifiers getAccess = 0, setAccess = 0;
			DoParsePunct("{");
			DoParseAccessorDeclarations(name, ref hasGet, ref hasSet, ref getterBody, ref setterBody, ref getAttrs, ref setAttrs, ref getAccess, ref setAccess);
			if (!m_scanner.Token.IsPunct("}"))
				DoParseAccessorDeclarations(name, ref hasGet, ref hasSet, ref getterBody, ref setterBody, ref getAttrs, ref setAttrs, ref getAccess, ref setAccess);
			Token last = DoParsePunct("}");
		
			members.Add(new CsProperty(nameOffset, getterBody, setterBody, getAccess, setAccess, name, getAttrs, setAttrs, hasGet, hasSet, type, attrs, modifiers, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
		}
		
		private Token DoParsePunct(string symbol)
		{
			if (!m_scanner.Token.IsPunct(symbol))
				throw new CsParserException("Expected a '{0}' on line {1}, but found '{2}'", symbol, m_scanner.Token.Line, m_scanner.Token.Text());
			
			Token token = m_scanner.Token;
			m_scanner.Advance();
			
			return token;
		}
		
		// qualified-identifier:
		//     identifier
		//     qualified-identifier   .   identifier
		private string DoParseQualifiedIdentifier()
		{
			Token last = m_scanner.Token;
			string name = DoParseIdentifier(ref last);

			while (m_scanner.Token.IsPunct("."))
			{
				m_scanner.Advance();
				name += "." + DoParseIdentifier(ref last);
			}
			
			return name;
		}
		
		// return-type:
		//     type
		//     void
		private string DoParseReturnType()
		{
			return DoParseType();
		}
		
		// struct-body:
		//     {   struct-member-declarations?   }
		// 
		// struct-member-declarations:
		//     struct-member-declaration
		//     struct-member-declarations   struct-member-declaration
		private void DoParseStructBody(List<CsMember> members, List<CsType> types, ref Token first, ref Token last)
		{
			DoParsePunct("{");
			first = m_scanner.Token;
			DoParseClassMemberDeclaration(members, types);

			if (!m_try || m_scanner.Token.IsPunct("}"))
				last = DoParsePunct("}");
			else
				last = m_scanner.Token;
		}
		
		// struct-declaration:
		//     attributes?   struct-modifiers?   partial?   struct   identifier   type-parameter-list?
		//               struct-interfaces?   type-parameter-constraints-clauses?   struct-body  ;?
		private CsType DoParseStructDeclaration(CsAttribute[] attrs, MemberModifiers modifiers, Token first, MemberModifiers defaultAccess)
		{
			// partial?
			if (m_scanner.Token.IsIdentifier("partial"))
			{
				m_scanner.Advance();
				modifiers |= MemberModifiers.Partial;
			}
			
			// struct
			DoParseKeyword("struct");
			
			// identifier
			Token last = m_scanner.Token;
			int nameOffset = m_scanner.Token.Offset;
			string name = DoParseIdentifier(ref last);
			
			// type-parameter-list?
			string gargs = null;
			if (m_scanner.Token.IsPunct("<"))
			{
				gargs = DoScanBody("<", ">", ref last);
			}
			
			// struct-interfaces?
			CsBases interfaces = DoParseInterfaceTypeList(last);
			
			// type-parameter-constraints-clauses?   
			string constraints = DoParseTypeParameterConstraintsClauses();
			
			// struct-body  
			var members = new List<CsMember>();
			var types = new List<CsType>();
			Token open = m_scanner.Token;
			Token start = m_scanner.Token;
			DoParseStructBody(members, types, ref open, ref last);
			Token close = last;
			
			// ;?
			if (m_scanner.Token.IsPunct(";"))
			{
				last = m_scanner.Token;
				m_scanner.Advance();
			}
			
			CsBody body = new CsBody(name, start.Offset, open.Offset, close.Offset + close.Length - start.Offset, start.Line);
			return new CsStruct(nameOffset, body, members.ToArray(), types.ToArray(), interfaces, constraints, gargs, attrs, modifiers, name, first.Offset, last.Offset + last.Length - first.Offset, first.Line);
		}
		
		// The grammar for type: is fairly complex, but for our purposes it boils down to
		// namespace-or-type-name followed by optional "?" and rank-specifiers.
		// 
		// rank-specifiers:
		//     rank-specifier
		//     rank-specifiers   rank-specifier
		// 
		// rank-specifier:
		//     [   dim-separators?   ]
		private string DoParseType()
		{
			Token last = m_scanner.Token;
			string type = DoParseNamespaceOrTypeName(ref last);
			
			while (m_scanner.Token.IsPunct("[") || m_scanner.Token.IsPunct("?") || m_scanner.Token.IsPunct("*"))
			{
				if (m_scanner.Token.IsPunct("["))
				{
					type += "[" + DoScanBody("[", "]", ref last) + "]";
				}
				else
				{
					type += m_scanner.Token.Text();
					m_scanner.Advance();
				}
			}

			return type;
		}

		// type-declaration:
		//      class-declaration				partial?   class
		//      struct-declaration			partial?   struct
		//      interface-declaration		partial?   interface
		//      enum-declaration			enum
		//      delegate-declaration		delegate
		private void DoParseTypeDeclaration(List<CsMember> members, List<CsType> types, MemberModifiers defaultAccess)
		{
			// All members and types start with optional attributes and modifiers.
			Token first = m_scanner.Token;
			CsAttribute[] attrs = DoParseAttributes();
			MemberModifiers modifiers = DoParseModifiers();
			
			DoParseTypeDeclarationStub(first, attrs, modifiers, members, types, defaultAccess);
		}
		
		private void DoParseTypeDeclarationStub(Token first, CsAttribute[] attrs, MemberModifiers modifiers, List<CsMember> members, List<CsType> types, MemberModifiers defaultAccess)
		{
			if (m_scanner.Token.IsIdentifier("enum"))
			{
				m_scanner.Advance();
				CsMember member = DoParseEnumDeclaration(attrs, modifiers, first, defaultAccess);
				members.Add(member);
			}
			else if (m_scanner.Token.IsIdentifier("delegate"))
			{
				m_scanner.Advance();
				CsMember member = DoParseDelegateDeclaration(attrs, modifiers, first, defaultAccess);
				members.Add(member);
			}
			else if (m_scanner.Token.IsIdentifier("interface") || (m_scanner.Token.IsIdentifier("partial") && m_scanner.LookAhead(1).IsIdentifier("interface")))
			{
				CsType type = DoParseInterfaceDeclaration(attrs, modifiers, first, defaultAccess);
				types.Add(type);
			}
			else if (m_scanner.Token.IsIdentifier("struct") || (m_scanner.Token.IsIdentifier("partial") && m_scanner.LookAhead(1).IsIdentifier("struct")))
			{
				CsType type = DoParseStructDeclaration(attrs, modifiers, first, defaultAccess);
				types.Add(type);
			}
			else if (m_scanner.Token.IsIdentifier("class") || (m_scanner.Token.IsIdentifier("partial") && m_scanner.LookAhead(1).IsIdentifier("class")))
			{
				CsType type = DoParseClassDeclaration(attrs, modifiers, first, defaultAccess);
				types.Add(type);
			}
			else
				throw new CsParserException("Expected 'class', 'struct', 'interface', 'enum', 'delegate', or 'partial' on line {0}, but found '{1}'", m_scanner.Token.Line, m_scanner.Token.Text());			
		}
		
		// type-name:
		//      namespace-or-type-name
		private string DoParseTypeName(ref Token last)
		{
			return DoParseNamespaceOrTypeName(ref last);
		}
		
		// type-parameter:
		//      identifier
		private void DoParseTypeParameter()
		{
			Token last = m_scanner.Token;
			DoParseIdentifier(ref last);
		}
		
		// type-parameter-constraints-clauses:
		//      type-parameter-constraints-clause
		//      type-parameter-constraints-clauses   type-parameter-constraints-clause
		// 
		// type-parameter-constraints-clause:
		//      where   type-parameter   :   type-parameter-constraints
		private string DoParseTypeParameterConstraintsClauses()
		{
			string constraints = null;
			
			Token first = m_scanner.Token;
			Token last = m_scanner.Token;
			while (m_scanner.Token.IsIdentifier("where"))
			{
				m_scanner.Advance();
				
				DoParseTypeParameter();
				DoParsePunct(":");
				last = DoParseTypeParameterConstraints();
			}
			
			if (last.Offset != first.Offset)
				constraints = m_text.Substring(first.Offset, last.Offset + last.Length - first.Offset);
			
			return constraints;
		}
		
		// type-parameter-constraints:
		//     primary-constraint
		//     secondary-constraints
		//     constructor-constraint
		//     primary-constraint   ,   secondary-constraints
		//     primary-constraint   ,   constructor-constraint
		//     secondary-constraints   ,   constructor-constraint
		//     primary-constraint   ,   secondary-constraints   ,   constructor-constraint
		// 
		// primary-constraint:			
		//     class-type
		//     class
		//     struct
		// 
		// secondary-constraints:
		//     interface-type
		//     type-parameter
		//     secondary-constraints   ,   interface-type
		//     secondary-constraints   ,   type-parameter
		// 
		// constructor-constraint:
		//     new   (   )
		private Token DoParseTypeParameterConstraints()
		{
			Token last = m_scanner.Token;
			
			while (true)
			{
				DoParseNamespaceOrTypeName(ref last);
				
				if (m_scanner.Token.IsPunct("("))
				{
					m_scanner.Advance();
					last = DoParsePunct(")");
				}

				if (m_scanner.Token.IsPunct(","))
					m_scanner.Advance();
				else
					break;
			}
			
			return last;
		}
		
		// using-directives:
		//     using-directive
		//     using-directives   using-directive
		// 
		// using-directive:
		//     using-alias-directive
		//     using-namespace-directive
		// 
		// using-alias-directive:
		//     using   identifier   =   namespace-or-type-name   ;
		// 
		// using-namespace-directive:
		//     using   namespace-name   ;
		private void DoParseUsingDirectives(ref Token last, List<CsUsingAlias> aliases, List<CsUsingDirective> uses)
		{
			while (m_scanner.Token.IsIdentifier("using"))
			{
				Token first = m_scanner.Token;
				m_scanner.Advance();
				
				string name = DoParseNamespaceName();
				if (m_scanner.Token.IsPunct("="))
				{
					m_scanner.Advance();
					
					string value = DoParseNamespaceOrTypeName(ref last);
					last = DoParsePunct(";");
					
					aliases.Add(new CsUsingAlias(name, value, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
				}
				else
				{
					last = DoParsePunct(";");
					uses.Add(new CsUsingDirective(name, first.Offset, last.Offset + last.Length - first.Offset, first.Line));
				}
			}
		}
		
		private Token DoSkipBody(string open, string close, ref Token first, ref Token last)
		{
			if (!m_scanner.Token.IsPunct(open))
				throw new CsParserException("Expected a '{0}' on line {1}, but found '{2}'", open, m_scanner.Token.Line, m_scanner.Token.Text());
			
			int count = 1;
			m_scanner.Advance();
			first = m_scanner.Token;
			
			while (m_scanner.Token.IsValid() && count > 0)
			{
				if (m_scanner.Token.IsPunct(open))
					++count;
				else if (m_scanner.Token.IsPunct(close))
					--count;
				
				last = m_scanner.Token;
				m_scanner.Advance();
			}
			
			if (count > 0)
				throw new CsParserException("Expected a '{0}' to close line {1}, but found '{2}'", close, first.Line, m_scanner.Token.Text());
			
			return first;
		}
		
		// Returns a string containing the text between open and close punct
		// tokens while allowing for nested expressions. Last will be set to the
		// closing close token.
		private string DoScanBody(string open, string close, ref Token last)
		{
			Token f = m_scanner.Token;
			Token first = DoSkipBody(open, close, ref f, ref last);
			
			return m_text.Substring(first.Offset, last.Offset - first.Offset);
		}
		#endregion
		
		#region Fields
		private string m_text;
		private Scanner m_scanner;
		private bool m_try;
		private Token m_bad;
		#endregion
	}
}
