// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;

namespace Microsoft.AspNetCore.Mvc.Razor
{
    /// <summary>
    /// <para>
    /// A <see cref="TextWriter"/> that is backed by a unbuffered writer (over the Response stream) and/or a 
    /// <see cref="ViewBuffer"/>
    /// </para>
    /// <para>
    /// When <c>Flush</c> or <c>FlushAsync</c> is invoked, the writer copies all content from the buffer to
    /// the writer and switches to writing to the unbuffered writer for all further write operations.
    /// </para>
    /// </summary>
    public class RazorTextWriter : TextWriter
    {
        /// <summary>
        /// Creates a new instance of <see cref="RazorTextWriter"/>.
        /// </summary>
        /// <param name="unbufferedWriter">The <see cref="TextWriter"/> to write output to when this instance
        /// is no longer buffering.</param>
        /// <param name="buffer">The <see cref="ViewBuffer"/> to buffer output to.</param>
        /// <param name="encoder">The HTML encoder.</param>
        public RazorTextWriter(TextWriter unbufferedWriter, ViewBuffer buffer, HtmlEncoder encoder)
        {
            UnbufferedWriter = unbufferedWriter;
            Buffer = buffer;
            HtmlEncoder = encoder;

            BufferedWriter = new HtmlContentWrapperTextWriter(buffer, unbufferedWriter.Encoding);
            TargetWriter = BufferedWriter;
        }

        public RazorTextWriter(HtmlContentWrapperTextWriter writer, HtmlEncoder encoder)
        {
            BufferedWriter = writer;
            Buffer = writer.ContentBuilder;
            HtmlEncoder = encoder;

            TargetWriter = BufferedWriter;
            UnbufferedWriter = writer;
        }

        /// <inheritdoc />
        public override Encoding Encoding
        {
            get { return BufferedWriter.Encoding; }
        }

        /// <inheritdoc />
        public bool IsBuffering { get; private set; } = true;

        /// <summary>
        /// Gets the buffered content.
        /// </summary>
        public ViewBuffer Buffer { get; }

        // Internal for unit testing
        public HtmlContentWrapperTextWriter BufferedWriter { get; }

        public TextWriter UnbufferedWriter { get; }

        public TextWriter TargetWriter { get; set; }

        private HtmlEncoder HtmlEncoder { get; }

        /// <inheritdoc />
        public override void Write(char value)
        {
            TargetWriter.Write(value);
        }

        /// <inheritdoc />
        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (count < 0 || (index + count > buffer.Length))
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            TargetWriter.Write(buffer, index, count);
        }

        /// <inheritdoc />
        public override void Write(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                TargetWriter.Write(value);
            }
        }

        /// <inheritdoc />
        public override void Write(object value)
        {
            IHtmlContentBuilder builder;
            IHtmlContent content;
            if ((builder = value as IHtmlContentBuilder) != null)
            {
                Write(builder);
            }
            else if ((content = value as IHtmlContent) != null)
            {
                Write(content);
            }
            else
            {
                base.Write(value);
            }
        }

        /// <summary>
        /// Writes an <see cref="IHtmlContent"/> value.
        /// </summary>
        /// <param name="value">The <see cref="IHtmlContent"/> value.</param>
        public void Write(IHtmlContent value)
        {
            if (IsBuffering)
            {
                Buffer.AppendHtml(value);
            }
            else
            {
                value.WriteTo(TargetWriter, HtmlEncoder);
            }
        }

        /// <summary>
        /// Writes an <see cref="IHtmlContentBuilder"/> value.
        /// </summary>
        /// <param name="value">The <see cref="IHtmlContentBuilder"/> value.</param>
        public void Write(IHtmlContentBuilder value)
        {
            if (IsBuffering)
            {
                value.MoveTo(Buffer);
            }
            else
            {
                value.WriteTo(TargetWriter, HtmlEncoder);
            }
        }

        /// <inheritdoc />
        public override Task WriteAsync(char value)
        {
            return TargetWriter.WriteAsync(value);
        }

        /// <inheritdoc />
        public override Task WriteAsync(char[] buffer, int index, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (count < 0 || (buffer.Length - index < count))
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            return TargetWriter.WriteAsync(buffer, index, count);
        }

        /// <inheritdoc />
        public override Task WriteAsync(string value)
        {
            return TargetWriter.WriteAsync(value);
        }

        /// <inheritdoc />
        public override void WriteLine()
        {
            TargetWriter.WriteLine();
        }

        /// <inheritdoc />
        public override void WriteLine(string value)
        {
            TargetWriter.WriteLine(value);
        }

        /// <inheritdoc />
        public override Task WriteLineAsync(char value)
        {
            return TargetWriter.WriteLineAsync(value);
        }

        /// <inheritdoc />
        public override Task WriteLineAsync(char[] value, int start, int offset)
        {
            return TargetWriter.WriteLineAsync(value, start, offset);
        }

        /// <inheritdoc />
        public override Task WriteLineAsync(string value)
        {
            return TargetWriter.WriteLineAsync(value);
        }

        /// <inheritdoc />
        public override Task WriteLineAsync()
        {
            return TargetWriter.WriteLineAsync();
        }

        /// <summary>
        /// Copies the buffered content to the unbuffered writer and invokes flush on it.
        /// Additionally causes this instance to no longer buffer and direct all write operations
        /// to the unbuffered writer.
        /// </summary>
        public override void Flush()
        {
            if (IsBuffering)
            {
                IsBuffering = false;
                TargetWriter = UnbufferedWriter;
                Buffer.WriteTo(UnbufferedWriter, HtmlEncoder);
            }

            UnbufferedWriter.Flush();
        }

        /// <summary>
        /// Copies the buffered content to the unbuffered writer and invokes flush on it.
        /// Additionally causes this instance to no longer buffer and direct all write operations
        /// to the unbuffered writer.
        /// </summary>
        /// <returns>A <see cref="Task"/> that represents the asynchronous copy and flush operations.</returns>
        public override Task FlushAsync()
        {
            if (IsBuffering)
            {
                IsBuffering = false;
                TargetWriter = UnbufferedWriter;
                Buffer.WriteTo(UnbufferedWriter, HtmlEncoder);
            }

            return UnbufferedWriter.FlushAsync();
        }
    }
}