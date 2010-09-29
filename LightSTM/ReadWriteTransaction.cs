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
    /// Transaction strategy for read-write and read-only transactions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy should be used if the outermost transaction is read-write.
    /// For read-only outermost transactions <see cref="ReadOnlyTransaction"/>
    /// is faster.
    /// </para><para>
    /// This class is not thread safe. All calls are expected to be made
    /// by the thread that 'owns' the transaction.
    /// </para>
    /// </remarks>
    internal class ReadWriteTransaction : ITransaction
    {

        /// <summary>
        /// Stores information for one of the nested transactions.
        /// </summary>
        private struct InnerTransaction
        {
            private readonly bool _isReadOnly;
            private readonly HashSet<TV> _readSet;
            private readonly SortedDictionary<TV, object> _writeSet;

            /// <summary>
            /// Creates a new transaction.
            /// </summary>
            /// <param name="isReadOnly">If this transaction will be read-only.</param>
            public InnerTransaction(bool isReadOnly)
            {
                _isReadOnly = isReadOnly;
                _readSet = new HashSet<TV>();
                if (isReadOnly)
                {
                    _writeSet = null;
                }
                else
                {
                    _writeSet = new SortedDictionary<TV, object>(VComparer.Instance);
                }
            }

            /// <summary>
            /// Gets if this transaction is read-only.
            /// </summary>
            public bool IsReadOnly { get { return _isReadOnly; } }

            /// <summary>
            /// Gets the set with all variables that have been read in this transaction so far.
            /// </summary>
            /// <remarks>
            /// This does include variables read by previously commited inner transactions, but not the
            /// variables read by a currently running nested transaction.
            /// </remarks>
            public HashSet<TV> ReadSet { get { return _readSet; } }

            /// <summary>
            /// Gets the set of all variables that have been written in this transaction so far (including the written value).
            /// </summary>
            /// <remarks>
            /// <para>
            /// This does include variables written by previously commited inner transactions, but not the
            /// variables written by a currently running nested transaction.
            /// </para><para>
            /// If <c>IsReadOnly</c> is <c>true</c>, this property is <c>null</c>.
            /// </para>
            /// </remarks>
            public SortedDictionary<TV, object> WriteSet { get { return _writeSet; } }
        }

        /// <summary>
        /// All currently running transactions.
        /// </summary>
        /// <remarks>
        /// <para>If empty, no transaction is running.</para>
        /// <para>The first element is the outermost transaction, the last the currently executed transaction.</para>
        /// </remarks>
        private readonly List<InnerTransaction> _transactionStack = new List<InnerTransaction>();

        /// <summary>
        /// Global version this transaction reads from.
        /// </summary>
        /// <remarks>
        /// The read version is the same for all nested transactions.
        /// </remarks>
        private long _readVersion;
        
        /// <summary>
        /// Comparer to sort the write variables before locking.
        /// </summary>
        /// <remarks>
        /// Instances of this type are immutable. There is only a single instance.
        /// </remarks>
        private sealed class VComparer : IComparer<TV>
        {

            /// <summary>
            /// Use the singleton instance <c>Instance</c> instead.
            /// </summary>
            private VComparer()
            {
                // empty
            }

            /// <summary>
            /// Comares two variables.
            /// </summary>
            /// <remarks>
            /// The order is arbitrary, but it is guaranteed not to change.
            /// </remarks>
            /// <param name="x">First variable.</param>
            /// <param name="y">Second variable.</param>
            /// <returns></returns>
            public int Compare(TV x, TV y)
            {
                if (x == null) throw new ArgumentNullException("x");
                if (y == null) throw new ArgumentNullException("x");
                return Math.Sign(x.Id - y.Id);
            }

            /// <summary>
            /// Instance of this comparer.
            /// </summary>
            public static readonly VComparer Instance = new VComparer();
        }
        
        #region ITransaction Members

        /// <summary>
        /// Starts a new transaction with read and write support.
        /// </summary>
        /// <exception cref="ReadOnlyTransactionException">Thrown if there is a read-only host transaction.</exception>
        /// <remarks>
        /// Transactions can be nested. This method will start an inner transaction if another one is already running.
        /// </remarks>
        public void Begin()
        {
            if (_transactionStack.Count == 0)
            {
                // We start an outermost transaction. We'll read the current version of the memory..
                _readVersion = Interlocked.Read(ref TransactionManager.__globalClock);
            }
            else
            {
                // We start a nested transaction. Check that the host isn't read-only.
                if (_transactionStack[_transactionStack.Count - 1].IsReadOnly)
                {
                    throw new ReadOnlyTransactionException(ExceptionMessages.BeginReadWriteInReadonlyTransaction);
                }
            }

            _transactionStack.Add(new InnerTransaction(false));
        }

        /// <summary>
        /// Starts a new read-only transaction.
        /// </summary>
        /// <remarks>
        /// Transactions can be nested. This method will start an inner transaction if another one is already running.
        /// </remarks>
        public void BeginReadOnly()
        {
            if (_transactionStack.Count == 0)
            {
                // We start an outermost transaction. We'll read the current version of the memory.
                _readVersion = Interlocked.Read(ref TransactionManager.__globalClock);
            }

            _transactionStack.Add(new InnerTransaction(true));
        }

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
        public void Commit()
        {
            if (_transactionStack.Count == 0) throw new OutsideOfTransactionException(ExceptionMessages.InternalError);

            // currently running transaction
            var transaction = _transactionStack[_transactionStack.Count - 1];

            if (_transactionStack.Count > 1)
            {
                // Just an inner transaction is commited. No real change is performed.

                var hostTransaction = _transactionStack[_transactionStack.Count - 2];

                // Copy the transaction's actions to the host transaction.
                // This makes the actions of this transaction visible to the host.
                foreach (var variable in transaction.ReadSet)
                {
                    hostTransaction.ReadSet.Add(variable);
                }
                if (!transaction.IsReadOnly)
                {
                    foreach (var writePair in transaction.WriteSet)
                    {
                        hostTransaction.WriteSet[writePair.Key] = writePair.Value;
                    }
                }
            }
            else
            {
                // Outermost transaction is commited. Perform changes.

                // Lock all written variables.
                // Since the set is sorted, no dead-locks can occur.
                foreach (TV variable in transaction.WriteSet.Keys)
                {
                    AquireLock(variable);
                }

                // Get us a version.
                long _writeVersion = Interlocked.Increment(ref TransactionManager.__globalClock);

                // Ensure proper serializability by checking that no other transaction
                // has written to the variables our results (written variables) depend on.
                foreach (TV variable in transaction.ReadSet)
                {

                    // We need to check the current version of the variable.
                    // If the variable is locked, then somebody is writing to it, thous the
                    // version will be larger than our read version once the variable is unlocked.
                    // We therefore don't need to wait for that to happen and just trigger a conflict
                    // (except of course if the variable is locked by us).

                    long version;
                    // TODO: checking this only for locked variables would be faster.
                    // TODO: maybe it could be made even faster by checking this case during locking.
                    if (transaction.WriteSet.ContainsKey(variable))
                    {
                        version = TransactionManager.Unlock(Interlocked.Read(ref variable.__vLock));
                    }
                    else
                    {
                        version = Interlocked.Read(ref variable.__vLock);
                    }

                    // abort if another thread has written to it since our transaction started, or is about to do this.
                    if (version > _readVersion || TransactionManager.IsLocked(version))
                    {
                        foreach (TV writeVar in transaction.WriteSet.Keys)
                        {
                            ReleaseLock(writeVar);
                        }
                        throw new ConflictException();
                    }
                }

                // At this point we know that the commit will be sucessful. 
                // Write the write operations out to memory and release the lock.

                foreach (KeyValuePair<TV, object> pair in transaction.WriteSet)
                {
                    TV variable = pair.Key;
                    object value = pair.Value;

                    variable.__Value = value;
                    Interlocked.Exchange(ref variable.__vLock, _writeVersion);
                }
            }

            // pop
            _transactionStack.RemoveAt(_transactionStack.Count - 1);

            // inform listeners
            if (_transactionStack.Count == 0 || !transaction.IsReadOnly)
            {
                foreach (TV variable in transaction.WriteSet.Keys)
                {
                    variable.OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Aborts the current transaction.
        /// </summary>
        /// <exception cref="OutsideOfTransactionException">Thrown if no transaction is running.</exception>
        /// <remarks>
        /// In case of nested transactions, only the inner-most transaction is rolled back.
        /// </remarks>
        public void Rollback()
        {
            if (_transactionStack.Count == 0) throw new OutsideOfTransactionException(ExceptionMessages.InternalError);

            // We just pop the transaction.
            _transactionStack.RemoveAt(_transactionStack.Count - 1);
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
            get { return _transactionStack.Count > 0; }
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
            if (_transactionStack.Count == 0) throw new OutsideOfTransactionException();

            object value;

            // Go down the stack and see if the variable was previously written, if so
            // return that value since this write is visible to us.
            for (int i = _transactionStack.Count - 1; i >= 0; i--)
            {
                if (!_transactionStack[i].IsReadOnly && _transactionStack[i].WriteSet.TryGetValue(variable, out value))
                {
                    return value;
                }
            }


            // Read current value from memory and check if nobody has written to it since we started reading.

            // receive valid value and version (spinlock)
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

            // Memorize that we have read this variable.
            _transactionStack[_transactionStack.Count - 1].ReadSet.Add(variable);
            return value;
        }

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
        public void Write(TV variable, object value)
        {
            if (_transactionStack.Count == 0) throw new OutsideOfTransactionException();

            // peek
            var transaction = _transactionStack[_transactionStack.Count - 1];

            if (transaction.IsReadOnly) throw new ReadOnlyTransactionException(ExceptionMessages.WriteInReadonlyTransaction);

            // Memorize the write operation. It will be executed once the outermost transaction is commited.
            transaction.WriteSet[variable] = value;
        }

        #endregion

        /// <summary>
        /// Spinning lock aquirement.
        /// </summary>
        /// <remarks>
        /// Has no timeout.
        /// </remarks>
        /// <param name="variable">Variable to lock.</param>
        private static void AquireLock(TV variable)
        {
            while (true)
            {
                long beforeLocking = Interlocked.Read(ref variable.__vLock);
                if (!TransactionManager.IsLocked(beforeLocking))
                {
                    long afterLocking = TransactionManager.Lock(beforeLocking);
                    // exchange with the locked value but only if nothing else happened in the mean time.
                    long originalVLock = Interlocked.CompareExchange(ref variable.__vLock, afterLocking, beforeLocking);
                    if (originalVLock == beforeLocking)
                    {
                        // Exchange happened. We have the lock.
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <param name="variable">Variable to unlock.</param>
        private static void ReleaseLock(TV variable)
        {
            long vLock = Interlocked.Read(ref variable.__vLock);
            vLock = TransactionManager.Unlock(vLock);
            Interlocked.Exchange(ref variable.__vLock, vLock);
        }
    }
}
