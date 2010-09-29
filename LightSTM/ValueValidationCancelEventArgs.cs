/* Copyright (c) 2010 Stefan C. Mueller
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
using System.ComponentModel;

namespace TransactionalVariable
{

    /// <summary>
    /// Provides data for the validation event.
    /// </summary>
    /// <typeparam name="T">Type of the value stored in the validated variable.</typeparam>
    public sealed class ValueValidationCancelEventArgs<T> : CancelEventArgs
    {
        private readonly TV<T> _variable;
        private readonly T _newValue;

        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="variable">Variable which the new value will be assigned to on success. Must not be <c>null</c>.</param>
        /// <param name="newValue">Value which the new value will be assigned to on success.</param>
        public ValueValidationCancelEventArgs(TV<T> variable, T newValue)
        {
            if (variable == null) throw new ArgumentNullException("variable");
            _variable = variable;
            _newValue = newValue;
        }

        /// <summary>
        /// Gets the value to which the new value will be assigned to on success.
        /// </summary
        public TV<T> Variable { get { return _variable; } }

        /// <summary>
        /// New value which will be assigned to the variable on success.
        /// </summary>
        public T NewValue { get { return _newValue; } }

    }
}
