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
using NUnit.Framework;
using TransactionalVariable;

namespace TransactionalVariableTest
{

    /// <summary>
    /// Tests for nested transactions with a single thread.
    /// </summary>
    [TestFixture]
    public class SingleThreadNestedTransactionTests
    {

        /// <summary>
        /// Ensure that each test finishes properly. Otherwise errors may leak into other tests.
        /// </summary>
        [TearDown]
        public void AllTransactionsHaveClosed()
        {
            Assert.IsFalse(TransactionManager.TransactionRunning, "A transaction was still running after the test");
        }

        /// <summary>
        /// Read witin two nested read-write transactions.
        /// </summary>
        [Test]
        public void RWRWRead()
        {
            var variable = new TV<int>(42);
            var actual = TransactionManager.Run(() =>
            {
                return TransactionManager.Run(() =>
                {
                    return variable.Value;
                });
            });
            Assert.AreEqual(42, actual);
        }

        /// <summary>
        /// Write witin two nested read-write transactions.
        /// </summary>
        [Test]
        public void RWRWWrite()
        {
            var variable = new TV<int>(0);
            TransactionManager.Run(() =>
            {
                TransactionManager.Run(() =>
                {
                    variable.Value = 42;
                });
            });
            int actual = TransactionManager.Run(() => variable.Value);
            Assert.AreEqual(42, actual);
        }

        /// <summary>
        /// Read-only transactions must be possible within read-write transactions.
        /// </summary>
        [Test]
        public void ReadOnlyWithinWrite()
        {
            var variable = new TV<int>(42);
            var actual = TransactionManager.Run(() =>
            {
                return TransactionManager.RunReadOnly(() =>
                {
                    return variable.Value;
                });
            });
            Assert.AreEqual(42, actual);
        }

        /// <summary>
        /// Read-write transactions must not be allowed within read-only transactions.
        /// </summary>
        [Test]
        [ExpectedException(typeof(ReadOnlyTransactionException))]
        public void ReadWriteWithinReadOnly()
        {
            var variable = new TV<int>(42);
            var actual = TransactionManager.RunReadOnly(() =>
            {
                return TransactionManager.Run(() =>
                {
                    return variable.Value;
                });
            });
        }

        /// <summary>
        /// Read-write transactions must not be allowed within read-only transactions even
        /// if the root transaction is read-write.
        /// </summary>
        [Test]
        [ExpectedException(typeof(ReadOnlyTransactionException))]
        public void ReadWriteWithinReadOnlyWithinReadWrite()
        {
            var variable = new TV<int>(42);

            TransactionManager.Run(() =>
            {
                var actual = TransactionManager.RunReadOnly(() =>
                {
                    return TransactionManager.Run(() =>
                    {
                        return variable.Value;
                    });
                });
            });
        }

        /// <summary>
        /// Nested read-only transactions must not allow write actions.
        /// </summary>
        [Test]
        [ExpectedException(typeof(ReadOnlyTransactionException))]
        public void WriteWithinNestedReadOnly()
        {
            var variable = new TV<int>(42);
            TransactionManager.Run(() =>
            {
                return TransactionManager.RunReadOnly(() =>
                {
                    variable.Value = 42;
                    return 0;
                });
            });
        }

        /// <summary>
        /// Previous (uncommited) writes of the host transaction are visible.
        /// </summary>
        [Test]
        public void HostChangesAreVisible()
        {
            var variable = new TV<int>(0);
            int actual = TransactionManager.Run(() =>
            {
                variable.Value = 42;
                return TransactionManager.Run(() =>
                {
                    return variable.Value;
                });
            });
            Assert.AreEqual(42, actual);
        }

        /// <summary>
        /// Writes of rollbacked nested transactions are not visible to the host.
        /// </summary>
        [Test]
        public void RollbackedChangesAreInvisible()
        {
            var variable = new TV<int>(0);
            int actual = TransactionManager.Run(() =>
            {
                try
                {
                    TransactionManager.Run(() =>
                    {
                        variable.Value = 42;
                        throw new InvalidCastException("within nested transaction");
                    });
                }
                catch (InvalidCastException) { }
                return variable.Value;
            });
            Assert.AreEqual(0, actual);
        }

        /// <summary>
        /// Writes of commited nested transactions are not visible to the host.
        /// </summary>
        [Test]
        public void CommitedChangesAreVisible()
        {
            var variable = new TV<int>(0);
            int actual = TransactionManager.Run(() =>
            {

                TransactionManager.Run(() =>
                {
                    variable.Value = 42;
                });
                return variable.Value;
            });
            Assert.AreEqual(42, actual);
        }

        /// <summary>
        /// Tests that a validation failure does cause an exception.
        /// </summary>
        [Test]
        [ExpectedException(typeof(ValidationFailedException))]
        public void ValidationFails()
        {
            var variable = new TV<int>(0);
            variable.Validation += (sender, e) => e.Cancel = true;
            TransactionManager.Run(() =>
            {
                variable.Value = 1;
            });
        }

        /// <summary>
        /// Tests that a validation failure does cause a rollback.
        /// </summary>
        [Test]
        public void ValidationFailureRollback()
        {
            var variable = new TV<int>(0);
            variable.Validation += (sender, e) => e.Cancel = true;
            try
            {
                TransactionManager.Run(() =>
                {
                    variable.Value = 1;
                });
            }
            catch { }
            int actual = TransactionManager.Run(() => variable.Value);
            Assert.AreEqual(0, actual);
        }

        /// <summary>
        /// Tests that successful validation does not cause a rollback or an exception.
        /// </summary>
        [Test]
        public void ValidationSuccess()
        {
            var variable = new TV<int>(0);
            variable.Validation += (sender, e) => { };
            TransactionManager.Run(() =>
            {
                variable.Value = 1;
            });
            int actual = TransactionManager.Run(() => variable.Value);
            Assert.AreEqual(1, actual);
        }

        /// <summary>
        /// Tests that the transaction runs in a transaction.
        /// </summary>
        [Test]
        public void ValidationInTransaction()
        {
            var variable = new TV<int>(0);
            variable.Validation += (sender, e) => { Assert.IsTrue(TransactionManager.TransactionRunning); };
            TransactionManager.Run(() =>
            {
                variable.Value = 1;
            });
        }

        /// <summary>
        /// Tests that the changed event is raised.
        /// </summary>
        [Test]
        public void PropertyChangedRaised()
        {
            int called = 0;
            var variable = new TV<int>(0);
            variable.PropertyChanged += (sender, e) => called++;
            TransactionManager.Run(() =>
            {
                variable.Value = 1;
            });
            Assert.AreEqual(1, called);
        }

        /// <summary>
        /// Tests that the changed event is raised outside of a transaction.
        /// </summary>
        [Test]
        public void PropertyChangedOutsideTransaction()
        {
            var variable = new TV<int>(0);
            variable.PropertyChanged += (sender, e) => { Assert.IsFalse(TransactionManager.TransactionRunning); };
            TransactionManager.Run(() =>
            {
                variable.Value = 1;
            });
        }


    }
}
