﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;


namespace Fuzzy.Components
{
	/// <summary>
	/// Automatically generates <see cref="RenderTreeBuilder"/> calls in a fluent style whilst
	/// taking care of newlines and indentation if the <c>prettyPrint</c> constructor argument
	/// is <c>true</c>, as well as automatically generated source line based sequence numbers
	/// for all nodes in the render tree.
	/// </summary>
	/// <remarks>
	/// <see cref="IDisposable"/> is implemented in order to provide automatic verification of
	/// balancing of <see cref="Region"/>, <see cref="Element"/>, and <see cref="Component"/>
	/// calls with <see cref="Close"/> calls, if required.
	/// <para>
	/// Sequence numbers generated are 10 times the source code line number from where each
	/// method is called, which allows for multiple nodes on the same line as well as additional
	/// nodes auto-generated from high level nodes such as <see cref="Div>"/>, in which case the
	/// additional nodes will be given incremental sequence numbers.
	/// </para>
	/// </remarks>
	/// <example>
	/// In the following usage:
	/// <code>
	/// line 19:        protected override void BuildRenderTree(RenderTreeBuilder builder)
	/// line 20:           => builder.Build()
	/// line 21:               .Div("sidebar")
	/// line 22:                   .Component&lt;NavMenu&gt;()
	/// line 23:               .Close()
	/// line 24:               .NewLine();
	/// line 25:               ...
	/// </code>
	/// The <c>builder.OpenElement()</c> call generated by the <see cref="Div"/> method call
	/// on line 21 will be given sequence number <c>210</c> and the following
	/// <c>builder.AddAttribute("class", "sidebar")</c> call also generated by the <c>Div</c>
	/// method will be given sequence number 211, as it's generated from the same source line
	/// but must have a unique sequence number.
	/// <para>
	/// Next, the <c>builder.OpenComponent()</c> call generated by the <see cref="Component"/>
	/// method call on line 22 will be given sequence number <c>220</c> and the following
	/// <c>builder.CloseComponent()</c> call also generated by the <c>Component()</c> method
	/// will be given sequence number 221 as, again, it's generated from the same source line
	/// but must have a unique sequence number.
	/// </para><para>
	/// No sequence number will be generated for the <see cref="Close"/> method call
	/// on line 23 as a sequence parameter isn't required for the <c>builder.CloseElement</c>
	/// call, and finally, the <c>builder.AddMarkup()</c> call generated by the
	/// <see cref="NewLine"/> method call on line 24 will be given sequence number <c>240</c>.
	/// </para>
	/// </example>
	public class FluentRenderTreeBuilder: IDisposable
	{
		#region Helper Types

		enum BlockType
		{
			Component,
			Element,
			Region
		}

		#endregion Helper Types

		#region Fields

		readonly Stack<BlockType> _blocks = new Stack<BlockType>();
		readonly RenderTreeBuilder _builder = default!;
		readonly bool _prettyPrint;
		readonly int _initialIndent;
		readonly int _maxPerLine;
		readonly ILogger? _logger;

		int _currentLine;
		int _sequence;

		#endregion Fields

		#region Construction

		public FluentRenderTreeBuilder (RenderTreeBuilder builder,
					bool prettyPrint, int initialIndent, int maxPerLine, ILogger? logger = null)
		{
			_builder = builder;
			_prettyPrint = prettyPrint;
			_initialIndent = initialIndent;
			_maxPerLine = maxPerLine;
			_logger = logger;
		}

		public void Dispose()
		{
			if (_blocks.Any())
				throw new InvalidOperationException("Unbalanced Close calls");

			PrettyPrint();
		}

		#endregion Construction

		#region Methods

#pragma warning disable BL0006 // Do not use RenderTree types

		public FluentRenderTreeBuilder Attribute(RenderTreeFrame frame,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddAttribute(GetSequence(line), frame);
			return this;
		}

#pragma warning restore BL0006 // Do not use RenderTree types

		public FluentRenderTreeBuilder Attribute(string name, bool value,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddAttribute(GetSequence(line), name, value);
			return this;
		}

		public FluentRenderTreeBuilder Attribute(string name, EventCallback callback,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddAttribute(GetSequence(line), name, callback);
			return this;
		}

		public FluentRenderTreeBuilder Attribute(string name, MulticastDelegate @delegate,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddAttribute(GetSequence(line), name, @delegate);
			return this;
		}

		public FluentRenderTreeBuilder Attribute(string name, object value,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddAttribute(GetSequence(line), name, value);
			return this;
		}

		public FluentRenderTreeBuilder Attribute(string name, string value,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddAttribute(GetSequence(line), name, value);
			return this;
		}

		public FluentRenderTreeBuilder Attribute<TArgument>(string name, EventCallback<TArgument> callback,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddAttribute(GetSequence(line), name, callback);
			return this;
		}

		public FluentRenderTreeBuilder Component(Type type, [CallerLineNumber] int line = 0)
		{
			PrettyPrint(line);
			_builder.OpenComponent(GetSequence(line), type);
			PrettyPrint(line, offset: -1);
			_builder.CloseComponent();

			return this;
		}

		public FluentRenderTreeBuilder Component<TComponent>([CallerLineNumber] int line = 0)
			where TComponent : IComponent
		{
			PrettyPrint(line);
			_builder.OpenComponent<TComponent>(GetSequence(line));
			PrettyPrint(line, offset: -1);
			_builder.CloseComponent();

			return this;
		}

		public FluentRenderTreeBuilder ContentComponent(Type type, [CallerLineNumber] int line = 0)
		{
			PrettyPrint(line);
			_builder.OpenComponent(GetSequence(line), type);
			_blocks.Push(BlockType.Component);

			return this;
		}

		public FluentRenderTreeBuilder ContentComponent<TComponent>([CallerLineNumber] int line = 0)
			where TComponent : IComponent
		{
			PrettyPrint(line);
			_builder.OpenComponent<TComponent>(GetSequence(line));
			_blocks.Push(BlockType.Component);

			return this;
		}

		public FluentRenderTreeBuilder ComponentReferenceCapture(Action<object> action,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddComponentReferenceCapture(GetSequence(line), action);
			return this;
		}

		public FluentRenderTreeBuilder Content(MarkupString markup, bool prettyPrint = false,
				[CallerLineNumber] int line = 0)
		{
			if (prettyPrint)
				PrettyPrint(line);

			_builder.AddContent(GetSequence(line), markup);
			return this;
		}

		public FluentRenderTreeBuilder Content(object value, bool prettyPrint = false,
				[CallerLineNumber] int line = 0)
		{
			if (prettyPrint)
				PrettyPrint(line);

			_builder.AddContent(GetSequence(line), value);

			return this;
		}

		public FluentRenderTreeBuilder Content(RenderFragment fragment, bool prettyPrint = false,
				[CallerLineNumber] int line = 0)
		{
			if (prettyPrint)
				PrettyPrint(line);

			_builder.AddContent(GetSequence(line), fragment);
			return this;
		}

		public FluentRenderTreeBuilder Content(string value, bool prettyPrint = false,
				[CallerLineNumber] int line = 0)
		{
			if (prettyPrint)
				PrettyPrint(line);

			_builder.AddContent(GetSequence(line), value);

			return this;
		}

		public FluentRenderTreeBuilder Content<TValue>(RenderFragment<TValue>? fragment, TValue value,
				bool prettyPrint = false, [CallerLineNumber] int line = 0)
		{
			if (prettyPrint)
				PrettyPrint(line);

			_builder.AddContent<TValue>(GetSequence(line), fragment, value);

			return this;
		}

		public FluentRenderTreeBuilder Element(string name,
				[CallerLineNumber] int line = 0)
		{
			PrettyPrint(line);
			_builder.OpenElement(GetSequence(line), name);
			_blocks.Push(BlockType.Element);

			return this;
		}

		public FluentRenderTreeBuilder ElementReferenceCapture(Action<ElementReference>? action,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddElementReferenceCapture(GetSequence(line), action);
			return this;
		}

		public FluentRenderTreeBuilder Markup(string content, bool prettyPrint = false,
				[CallerLineNumber] int line = 0)
		{
			if (prettyPrint)
				PrettyPrint(line);

			_builder.AddMarkupContent(GetSequence(line), content);
			return this;
		}

		public FluentRenderTreeBuilder MultipleAttributes(
				IEnumerable<KeyValuePair<string, object>> attributes,
				[CallerLineNumber] int line = 0)
		{
			_builder.AddMultipleAttributes(GetSequence(line), attributes);
			return this;
		}

		public FluentRenderTreeBuilder Region([CallerLineNumber] int line = 0)
		{
			_builder.OpenRegion(GetSequence(line));
			_blocks.Push(BlockType.Region);

			return this;
		}

		public FluentRenderTreeBuilder Clear()
		{
			_builder.Clear();
			return this;
		}

		public ArrayRange<RenderTreeFrame> GetFrames()
			=> _builder.GetFrames();

		public FluentRenderTreeBuilder SetKey(object value)
		{
			_builder.SetKey(value);
			return this;
		}

		public FluentRenderTreeBuilder SetUpdatesAttributeName(string name)
		{
			_builder.SetUpdatesAttributeName(name);
			return this;
		}

		#region Shortcuts

		public FluentRenderTreeBuilder Class(string value, [CallerLineNumber] int line = 0)
		{
			_builder.AddAttribute(GetSequence(line), "class", value);
			return this;
		}

		/// <summary>
		/// Adds a number of newlines as markup content (defaulting to one), with optionally an
		/// indent following the last (or only) one, if <paramref name="prettyPrint"/> is true
		/// and pretty printing is enabled in the parent <see cref="Controller"/>.
		/// </summary>
		/// <remarks>
		/// This method is cosmetic, simply inserting line breaks in the generated markup.
		/// </remarks>
		/// <param name="number"></param>
		/// <param name="prettyPrint">
		/// If <c>true</c>, add an indent to the last (or only) newline, if pretty printing is
		/// enabled in the parent <see cref="Controller"/>.
		/// </param>
		/// <param name="line"></param>
		/// <returns></returns>
		public FluentRenderTreeBuilder NewLine(int number = 1, bool prettyPrint = false,
				[CallerLineNumber] int line = 0)
		{
			if (number == 0)
				return this;

			if (number == 1)
				_builder.AddMarkupContent(GetSequence(line), Environment.NewLine);
			else // STILL no `new string ("string", n)` in C#/.NET, so do it the tedious way :(
				_builder.AddMarkupContent(GetSequence(line),
					new string('X', number).Replace("X", Environment.NewLine));

			if (prettyPrint)
				PrettyPrint(line, indentOnly: true);

			return this;
		}

		#endregion Shortcuts

		/// <summary>
		/// Closes the current Region, Element or ContentComponent block.
		/// </summary>
		/// <remarks>
		/// Calls to this method must match calls to <see cref="Region(int)"/>,
		/// <see cref="Element(string, int)"/> and <see cref="Component(Type, int)"/>.
		/// </remarks>
		/// <returns></returns>
		public FluentRenderTreeBuilder Close(
				[CallerLineNumber] int line = 0)
		{
			var type = _blocks.Pop();
			if (type != BlockType.Region)
				PrettyPrint(line);

			switch (type)
			{
				case BlockType.Region: _builder.CloseRegion(); break;
				case BlockType.Element: _builder.CloseElement(); break;
				case BlockType.Component: _builder.CloseComponent(); break;
			};

			return this;
		}

		void PrettyPrint(int line = -1, int offset = 0, bool indentOnly = false)
		{
			if (!_prettyPrint)
				return;

			if (line == -1)
				line = _currentLine;

			_builder.AddMarkupContent(GetSequence(line),
					(indentOnly ? "" : Environment.NewLine) +
					new string('\t', _initialIndent + _blocks.Count + offset));
		}

		/// <summary>
		/// Gets the sequence number for the given source line.
		/// </summary>
		/// <remarks>
		/// See the main <see cref="FluentRenderTreeBuilder"/> information for details on
		/// sequence number generation and an example showing how it works.
		/// </remarks>
		/// <param name="line"></param>
		/// <returns></returns>
		int GetSequence(int line, [CallerMemberName] string callerName = "")
		{
			if (line < _currentLine)
				throw new InvalidOperationException("Line cannot be less than current line");

			if (line == _currentLine)
			{
				++_sequence;

				if (_sequence == (_currentLine + 1) * _maxPerLine)
					throw new InvalidOperationException(
							$"Only {_maxPerLine} operations allowed on one source line");
			}
			else
			{
				_currentLine = line;
				_sequence = line * _maxPerLine;
			}

			_logger?.LogInformation($"Sequence number {_sequence} generated for {callerName}");

			return _sequence;
		}

		#endregion Methods
	}
}
