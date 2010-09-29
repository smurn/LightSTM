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
using System.Threading;
using System.ComponentModel;

namespace TransactionalVariable
{

    /// <summary>
    /// Non-generic base class of all transactional memory variables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class has no public members. Using the generic sub-classes is encouraged.
    /// </para>
    /// <para>
    /// This class must not (and cannot) be extended directly. Extend from the generic type instead.
    /// </para>
    /// </remarks>
    public abstract class TV
    {
        private static long _nextId = 0;

        /// <summary>
        /// Unique identifier of this variable.
        /// </summary>
        private readonly long _id;

        /// <summary>
        /// Versionized lock. The highest bit is the lock bit, the remaining 63 bits are the version.
        /// </summary>
        internal long __vLock = 0;

        /// <summary>
        /// Creates an instance.
        /// </summary>
        protected internal TV()
        {
            _id = Interlocked.Increment(ref _nextId);
        }

        /// <summary>
        /// Allows direct access to the current value. For internal use only.
        /// </summary>
        internal abstract object __Value { get; set; }

        /// <summary>
        /// Unique identifier of this variable.
        /// </summary>
        internal long Id { get { return _id; } }

        /// <summary>
        /// Raises the PropertyChangedEvent.
        /// </summary>
        internal abstract void OnPropertyChanged();
    }


    /// <summary>
    /// Transactional memory variable.
    /// </summary>
    /// <remarks>
    /// A variable that can be accessed from within a transaction with ACID guarantees.
    /// </remarks>
    public class TV<T> : TV, INotifyPropertyChanged
    {

        /// <summary>
        /// Current value. Must onl6y be exposed by proper locking.
        /// </summary>
        private T _value;

        /// <summary>
        /// Event argument passed to the PropertyChanged event.
        /// </summary>
        private static readonly PropertyChangedEventArgs valuePropertyChangedEventArgs = new PropertyChangedEventArgs("Value");

        /// <summary>
        /// Creates a transactional memory variable that is initialized with the default value of its type.
        /// </summary>
        /// <remarks>
        /// New instances may be created inside or outside of transactions.
        /// </remarks>
        public TV()
        {
            _value = default(T);
        }

        /// <summary>
        /// Creates a transactional memory variable that is initialized with the given value.
        /// </summary>
        /// <param name="initialValue">Initial value assigned to this variable.</param>
        /// <remarks>
        /// New instances may be created inside or outside of transactions.
        /// </remarks>
        public TV(T initialValue)
        {
            _value = initialValue;
        }

        /// <summary>
        /// Gets or sets the value of this variable.
        /// </summary>
        /// <exception cref="OutsideOfTransactionException">Thrown if the variable is accessed outside of a transaction.</exception>
        /// <exception cref="NotSupportedException">Thrown if the transaction is read-only and an assignment operation was tried.</exception>
        /// <exception cref="ConflictException">Thrown as part of the normal operation. This exception must not be caught.</exception>
        /// <remarks>
        /// Within a transaction a value behaves as if it would be a single threaded application.
        /// </remarks>
        public T Value
        {
            get
            {
                return TransactionManager.Read(this);
            }
            set
            {
                if (Validate(value))
                {
                    TransactionManager.Write(this, value);
                }
                else
                {
                    throw new ValidationFailedException(ExceptionMessages.VariableValidationFailed);
                }
            }
        }

        /// <summary>
        /// Raised within the transaction during the assignment of a value to the <see cref="#Value"/> property.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each event handler has the chance to forbid the assignment by setting the <c>Cancel</c> flag of the
        /// event argument to <c>true</c>. This will cause the assignment to fail with a <see cref="ValidationFailedException"/>.
        /// </para>
        /// </remarks>
        public event EventHandler<ValueValidationCancelEventArgs<T>> Validation;

        /// <summary>
        /// Raised when a transaction was successfully commited that changed this variable.
        /// </summary>
        /// <remarks>
        /// The event will be risen outside of the transaction that caused the change.
        /// </remarks>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <c>PropertyChanged</c> event.
        /// </summary>
        internal override sealed void OnPropertyChanged()
        {
            if (PropertyChanged != null)
            {
                try
                {
                    PropertyChanged(this, valuePropertyChangedEventArgs);
                }
                catch
                {
                    // Property change events must not throw exceptions.
                    // TODO: log this or something
                }
            }
        }

        /// <summary>
        /// Allows direct access to the current value. For internal use only.
        /// </summary>
        internal override object __Value
        {
            get
            {
                return (object)_value;
            }
            set
            {
                _value = (T)value;
            }
        }

        /// <summary>
        /// Performs the validation.
        /// </summary>
        /// <param name="newValue">The new value that is assigned to the variable on success.</param>
        /// <returns>Returns <c>true</c> on successful validation.</returns>
        private bool Validate(T newValue)
        {
            if (Validation != null)
            {
                ValueValidationCancelEventArgs<T> e = new ValueValidationCancelEventArgs<T>(this, newValue);
                Validation(this, e);
                return !e.Cancel;
            }
            else
            {
                return true;
            }
        }

    }
}
