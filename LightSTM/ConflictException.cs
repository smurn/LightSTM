﻿/* Copyright (c) 2010 Stefan C. Mueller
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace TransactionalVariable
{

    /// <summary>
    /// For internal use only. Do not catch this exception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// During normal operation this exception is used internally.
    /// Catching this exception (without rethrowing) will result
    /// in undefined behaviour of the current transaction.
    /// </para>
    /// <para>
    /// This exception is public to make it possible for the user
    /// to explicitly pass it on (by catching and rethrowing).
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class ConflictException : Exception
    {

        /// <summary>
        /// Creates an instance with a default message.
        /// </summary>
        public ConflictException() : base (ExceptionMessages.ConflictAbortation)
        {
            // empty
        }

        /// <summary>
        /// Creates an instance with a specific message.
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        public ConflictException(string message)
            : base(message)
        {
            // empty
        }

                /// <summary>
        /// Creates an instance with a specific message and an inner exception.
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        /// <param name="inner">Exception that caused this exception.</param>
        public ConflictException(string message, Exception inner)
            : base(message, inner)
        {
            // empty
        }

        /// <summary>
        /// For deserialization.
        /// </summary>
        private ConflictException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            // empty
        }
    }
}
