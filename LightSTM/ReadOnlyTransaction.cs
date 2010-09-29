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

namespace TransactionalVariable
{

    /// <summary>
    /// Transaction strategy for read-only access.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy should be used if the outermost transaction is read-only.
    /// This allows a significantly faster implementation.
    /// </para><para>
    /// This class is not thread safe. All calls are expected to be made
    /// by the thread that 'owns' the transaction.
    /// </para>
    /// </remarks>
    internal class ReadOnlyTransaction : ITransaction
    {

        /// <summary>
        /// Global version this transaction reads from.
        /// </summary>
        private long _readVersion;

        /// <summary>
        /// Number of nested transactions.
        /// </summary>
        /// <remarks>
        /// If 0, no transaction is running.
        /// </remarks>
        private int _nestedTransactions;

        #region ITransaction Members

        /// <summary>
        /// Starts a new transaction with read and write support.
        /// </summary>
        /// <exception cref="ReadOnlyTransactionException">Thrown if there is a read-only host transaction.</exception>
        /// <remarks>
        /// Always throws a <see cref="ReadOnlyTransactionException"/> since this strategy does not support read-write
        /// transactions. The exception is even thrown if there is no host exception. This is a break of the
        /// <see cref="ITransaction"/> specification. The caller must avoid this from happening by choosing
        /// another implementation in this case.
        /// </remarks>
        public void Begin()
        {
            if (_nestedTransactions == 0)
            {
                // this should not happen since the caller should use another strategy.
                throw new ReadOnlyTransactionException(ExceptionMessages.InternalError);
            }
            else
            {
                throw new ReadOnlyTransactionException(ExceptionMessages.BeginReadWriteInReadonlyTransaction);
            }
        }

        /// <summary>
        /// Starts a new read-only transaction.
        /// </summary>
        /// <remarks>
        /// Transactions can be nested. This method will start an inner transaction if another one is already running.
        /// </remarks>
        public void BeginReadOnly()
        {
            if (_nestedTransactions == 0)
            {
                // We start an outermost transaction. We'll read the current version of the memory.
                _readVersion = Interlocked.Read(ref TransactionManager.__globalClock);

            } // else: Inner transactions must read the same version as the host transaction, otherwise isolation would break.

            _nestedTransactions++;
        }

        /// <summary>
        /// Tries to finish the current transaction.
        /// </summary>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <remarks>
        /// <para>
        /// In case of nested transactions, only the inner-most transaction is finished.
        /// </para>
        /// <para>
        /// Other than in the read-write case, commiting a read-only transaction cannot produce a conflict.
        /// </para><para>
        /// Since read-only transactions have no changes to commit or rollback both operations are identical.
        /// For future support, the caller should not make use of this property and carefully choose which
        /// one to call.
        /// </para>
        /// </remarks>
        public void Commit()
        {
            if (_nestedTransactions <= 0) throw new OutsideOfTransactionException(ExceptionMessages.InternalError);
            _nestedTransactions--;
        }

        /// <summary>
        /// Aborts the current transaction.
        /// </summary>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <remarks>
        /// <para>
        /// In case of nested transactions, only the inner-most transaction is finished.
        /// </para><para>
        /// Since read-only transactions have no changes to commit or rollback both operations are identical.
        /// For future support, the caller should not make use of this property and carefully choose which
        /// one to call.
        /// </para>
        /// </remarks>
        public void Rollback()
        {
            if (_nestedTransactions <= 0) throw new OutsideOfTransactionException(ExceptionMessages.InternalError);
            _nestedTransactions--;
        }

        /// <summary>
        /// Checks if a transaction is currently runnning.
        /// </summary>
        /// <returns>True if a transaction is running.</returns>
        /// <remarks>
        /// If this returns false then the transaction strategy may be replaced.
        /// </remarks>
        public bool TransactionRunning
        {
            get { return _nestedTransactions > 0; }
        }

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
        public object Read(TV variable)
        {
            if (_nestedTransactions <= 0) throw new OutsideOfTransactionException(ExceptionMessages.InternalError);
           
            // Read the variable, ensure that it is not changed or locked by spinning.
            object value;
            long preVLock;
            long postVLock;
            do
            {
                preVLock = Interlocked.Read(ref variable.__vLock);
                value = variable.__Value;
                postVLock = Interlocked.Read(ref variable.__vLock);
            } while (preVLock != postVLock || TransactionManager.IsLocked(preVLock));

            // abort if another thread has written to it since our transaction started
            if (preVLock > _readVersion) throw new ConflictException();

            return value;
        }

        /// <summary>
        /// Writes a value into a variable.
        /// </summary>
        /// <param name="variable">Variable to write to.</param>
        /// <param name="value">Value to store in the variable.</param>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <exception cref="ReadOnlyTransactionException">Thrown if called within a read-only transaction.</exception>
        /// <remarks>
        /// This method always throws an exception. Either a <c>OutsideOfTransactionException</c> if no
        /// transaction is running, or a <c>ReadOnlyTransactionException</c> if one is running (since all transactions
        /// used with this strategy are read-only.
        /// </remarks>
        public void Write(TV variable, object value)
        {
            if (_nestedTransactions <= 0) throw new OutsideOfTransactionException(ExceptionMessages.InternalError);
            throw new ReadOnlyTransactionException(ExceptionMessages.WriteInReadonlyTransaction);
        }

        #endregion
    }
}
