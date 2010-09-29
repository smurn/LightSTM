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

namespace TransactionalVariable
{

    /// <summary>
    /// Thread specific transaction strategy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each thread as (at most) one instance of this type assigned.
    /// The instance of a thread must only be changed if all transactions
    /// are closed.
    /// </para><para>
    /// Its implementation handles the transaction operations performed
    /// by that thread.
    /// </para>
    /// </remarks>
    internal interface ITransaction
    {

        /// <summary>
        /// Starts a new transaction with read and write support.
        /// </summary>
        /// <exception cref="ReadOnlyTransactionException">Thrown if there is a read-only host transaction.</exception>
        /// <remarks>
        /// Transactions can be nested. This method will start an inner transaction if another one is already running.
        /// </remarks>
        void Begin();

        /// <summary>
        /// Starts a new read-only transaction.
        /// </summary>
        /// <remarks>
        /// Transactions can be nested. This method will start an inner transaction if another one is already running.
        /// </remarks>
        void BeginReadOnly();

        /// <summary>
        /// Tries to finish the current transaction.
        /// </summary>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <exception cref="ConflictException">Thrown if a conflict is detected. 
        /// The transaction and all its host transactions must be roll-backed immediately. The outermost transaction
        /// may then be restarted.
        /// </exception>
        /// <remarks>
        /// In case of nested transactions, only the inner-most transaction is finished. Changes will be visible
        /// only to the host transaction. If the host transaction (or one of its hosts) rolls back, the changes
        /// of this transaction will be lost too.
        /// </remarks>
        void Commit();

        /// <summary>
        /// Aborts the current transaction.
        /// </summary>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <remarks>
        /// In case of nested transactions, only the inner-most transaction is rolled back.
        /// </remarks>
        void Rollback();

        /// <summary>
        /// Checks if a transaction is currently runnning.
        /// </summary>
        /// <returns>True if a transaction is running.</returns>
        /// <remarks>
        /// If this returns false then the transaction strategy may be replaced.
        /// </remarks>
        bool TransactionRunning { get; }

        /// <summary>
        /// Reads the value of a variable.
        /// </summary>
        /// <param name="variable">Variable to read from.</param>
        /// <returns>Value of the variable.</returns>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <exception cref="ConflictException">Thrown if a conflict is detected. 
        /// The transaction and all its host transactions must be roll-backed immediately. The outermost transaction
        /// may then be restarted.
        /// </exception>
        /// <remarks>
        /// ACID properties are guarnateed.
        /// </remarks>
        object Read(TV variable);

        /// <summary>
        /// Writes a value into a variable.
        /// </summary>
        /// <param name="variable">Variable to write to.</param>
        /// <param name="value">Value to store in the variable.</param>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <exception cref="TransactionConflict">Thrown if a conflict is detected. 
        /// The transaction and all its host transactions must be roll-backed immediately. The outermost transaction
        /// may then be restarted.
        /// <exception cref="ReadOnlyTransactionException">Thrown if called within a read-only transaction.</exception>
        /// <remarks>
        /// ACID properties are guarnateed.
        /// </remarks>
        void Write(TV variable, object value);

    }
}
