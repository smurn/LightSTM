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
    /// Runs code within a transaction.
    /// </summary>
    public static class TransactionManager
    {

        /// <summary>
        /// Global version clock.
        /// </summary>
        internal static long __globalClock = 0;

        /// <summary>
        /// Transaction of the current thread. 
        /// </summary>
        [ThreadStatic]
        private static ITransaction currentTransaction;

        /// <summary>
        /// Runs the given method in a transaction.
        /// </summary>
        /// <remarks>
        /// The given method may read and write TMVs.
        /// </remarks>
        /// <param name="transaction">Method to execute in a transaction. If it throws an exception, the transaction will be rolled back.
        /// See <see cref="TransactionManager"/> for the garantees that the 
        /// transaction gives to the code and for the rules that the method 
        /// must follow.</param>
        public static void Run(Action operation)
        {
            if (operation == null) throw new ArgumentNullException("operation");

            ITransaction transaction = currentTransaction;
            if (transaction == null || !transaction.TransactionRunning)
            {
                // we make the outermost transaction
                transaction = new ReadWriteTransaction();
                currentTransaction = transaction;
            }

            // loop until we got no conflicts
            while (true)    // we return directly on success
            {
                transaction.Begin();
                try
                {
                    operation();
                    transaction.Commit();
                    return;
                }
                catch (ConflictException)
                {
                    transaction.Rollback();
                    if (transaction.TransactionRunning) throw;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Runs the given method in a transaction.
        /// </summary>
        /// <typeparam name="T">Type of the return value.</typeparam>
        /// <param name="transaction">Method to execute in a transaction. If it throws an exception, the transaction will be rolled back.
        /// See <see cref="TransactionManager"/> for the garantees that the 
        /// transaction gives to the code and for the rules that the method 
        /// must follow.</param>
        /// <returns>Value returned by the given method.</returns>
        public static T Run<T>(Func<T> operation)
        {
            if (operation == null) throw new ArgumentNullException("operation");

            ITransaction transaction = currentTransaction;
            if (transaction == null || !transaction.TransactionRunning)
            {
                // we make the outermost transaction
                transaction = new ReadWriteTransaction();
                currentTransaction = transaction;
            }

            // loop until we got no conflicts
            while (true)    // we return directly on success
            {
                transaction.Begin();
                try
                {
                    T retVal = operation();
                    transaction.Commit();
                    return retVal;
                }
                catch (ConflictException) {
                    transaction.Rollback();
                    if (transaction.TransactionRunning) throw;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Runs the given method in a read-only transaction.
        /// </summary>
        /// <remarks>
        /// There is no way to run a read-only transaction without
        /// a return value. Since such a transaction cannot modify
        /// any state, the return value is the only way for a read-only
        /// transaction to have an effect.
        /// </remarks>
        /// <typeparam name="T">Type of the return value.</typeparam>
        /// <param name="transaction">Method to execute in a transaction. 
        /// See <see cref="TransactionManager"/> for the garantees that the 
        /// transaction gives to the code and for the rules that the method 
        /// must follow. In addition no TVM must be changed.</param>
        /// <returns>Value returned by the given method.</returns>
        public static T RunReadOnly<T>(Func<T> operation)
        {
            if (operation == null) throw new ArgumentNullException("operation");

            ITransaction transaction = currentTransaction;
            if (transaction == null || !transaction.TransactionRunning)
            {
                // we make the outermost transaction
                transaction = new ReadOnlyTransaction();
                currentTransaction = transaction;
            }

            // loop until we got no conflicts
            while (true)    // we return directly on success
            {
                transaction.BeginReadOnly();
                try
                {
                    T retVal = operation();
                    transaction.Commit();
                    return retVal;
                }
                catch (ConflictException)
                {
                    transaction.Rollback();
                    if (transaction.TransactionRunning) throw;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Gets if the current thread is currently running within a transaction.
        /// </summary>
        public static bool TransactionRunning
        {
            get
            {
                ITransaction transaction = currentTransaction;
                return transaction != null && transaction.TransactionRunning;
            }
        }

        /// <summary>
        /// Reads the value of a variable.
        /// </summary>
        /// <param name="variable">Variable to read from.</param>
        /// <returns>Value of the variable.</returns>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <exception cref="ConflictException">Thrown if a conflict is detected. The transaction is aborted automatically and should be restarted.</exception>
        /// <remarks>
        /// ACID properties are guarnateed.
        /// </remarks>
        internal static T Read<T>(TV<T> variable)
        {
            ITransaction transaction = currentTransaction;
            if (transaction == null || !transaction.TransactionRunning)
            {
                throw new OutsideOfTransactionException();
            }
            else
            {
                return (T)transaction.Read(variable);
            }
        }

        /// <summary>
        /// Writes a value into a variable.
        /// </summary>
        /// <param name="variable">Variable to write to.</param>
        /// <param name="value">Value to store in the variable.</param>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <exception cref="ConflictException">Thrown if a conflict is detected. The transaction is aborted automatically and should be restarted.</exception>
        /// <exception cref="NotSupportedException">Thrown if called within a read-only transaction.</exception>
        /// <remarks>
        /// ACID properties are guarnateed.
        /// </remarks>
        internal static void Write<T>(TV<T> variable, T value)
        {
            ITransaction transaction = currentTransaction;
            if (transaction == null || !transaction.TransactionRunning)
            {
                throw new OutsideOfTransactionException();
            }
            else
            {
                transaction.Write(variable, value);
            }
        }

        /// <summary>
        /// Checks if the lock bit is set.
        /// </summary>
        /// <param name="vLock">versionized lock to check.</param>
        /// <returns>True if the lock bit is set.</returns>
        internal static bool IsLocked(long vLock)
        {
            return (vLock & (1L << 63)) != 0u;
        }

        /// <summary>
        /// Sets the lock bit.
        /// </summary>
        /// <param name="unlocked">Versionized lock whos lock bit is to be set.</param>
        /// <returns>The given vLock with the lock bit set.</returns>
        internal static long Lock(long unlocked)
        {
            return unlocked | (1L << 63);
        }

        /// <summary>
        /// Clears the lock bit.
        /// </summary>
        /// <param name="locked">Versionized lock whos lock bit is to be cleared.</param>
        /// <returns>The given vLock with the lock bit cleared.</returns>
        internal static long Unlock(long locked)
        {
            return locked & ~(1L << 63);
        }

    }
}
