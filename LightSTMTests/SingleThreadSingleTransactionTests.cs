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
    /// Tests with only a single thread and no nested transactions.
    /// </summary>
    [TestFixture]
    public class SingleThreadSingleTransactionTests
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
        /// Constructor testing.
        /// </summary>
        [Test]
        public void CreateTMVWithoutTransaction()
        {
            new TV<int>();
        }

        /// <summary>
        /// Constructor testing.
        /// </summary>
        [Test]
        public void CreateTMVWithoutTransactionInitVal()
        {
            new TV<int>(42);
        }

        /// <summary>
        /// Do not allow variables to be accessed directly.
        /// </summary>
        [Test]
        [ExpectedException(typeof(OutsideOfTransactionException))]
        public void ReadWithoutTransaction()
        {
            var variable = new TV<int>(42);
            int dummy = variable.Value;
        }

        /// <summary>
        /// Do not allow variables to be accessed directly.
        /// </summary>
        [Test]
        [ExpectedException(typeof(OutsideOfTransactionException))]
        public void WriteWithoutTransaction()
        {
            var variable = new TV<int>();
            variable.Value = 42;
        }

        /// <summary>
        /// Tests the <c>TransactionManager.TransactionRunning</c> flag.
        /// </summary>
        [Test]
        public void TransactionRunningTrue()
        {
            var variable = new TV<int>(42);
            bool wasRunningInTransaction = TransactionManager.Run(() =>
            {
                return TransactionManager.TransactionRunning;
            });
            Assert.IsTrue(wasRunningInTransaction);
        }

        /// <summary>
        /// Tests the <c>TransactionManager.TransactionRunning</c> flag.
        /// </summary>
        [Test]
        public void TransactionRunningFalse()
        {
            Assert.IsFalse(TransactionManager.TransactionRunning);
        }

        /// <summary>
        /// Tests the <c>TransactionManager.TransactionRunning</c> flag.
        /// </summary>
        [Test]
        public void TransactionCloses()
        {
            var variable = new TV<int>(42);
            TransactionManager.Run(() => variable.Value);
            Assert.IsFalse(TransactionManager.TransactionRunning);
        }
        
        /// <summary>
        /// Single read operation.
        /// </summary>
        [Test]
        public void Read()
        {
            var variable = new TV<int>(42);
            int actual = TransactionManager.Run(() => variable.Value);
            Assert.AreEqual(42, actual);
        }

        /// <summary>
        /// Two read operations.
        /// </summary>
        [Test]
        public void ReadTwo()
        {
            var variableA = new TV<int>(42);
            var variableB = new TV<int>(43);
            int actual = TransactionManager.Run(() => variableB.Value - variableA.Value);
            Assert.AreEqual(1, actual);
        }

        /// <summary>
        /// Single write operation.
        /// </summary>
        [Test]
        public void Write()
        {
            var variable = new TV<int>();
            TransactionManager.Run(() => variable.Value = 42);
            int actual = TransactionManager.Run(() => variable.Value);
            Assert.AreEqual(42, actual);
        }

        /// <summary>
        /// Two write operations.
        /// </summary>
        [Test]
        public void WriteTwo()
        {
            var variableA = new TV<int>();
            var variableB = new TV<int>();
            TransactionManager.Run(() =>
            {
                variableA.Value = 42;
                variableB.Value = 43;
            });
            int actualA = TransactionManager.Run(() => variableA.Value);
            int actualB = TransactionManager.Run(() => variableB.Value);
            Assert.AreEqual(42, actualA);
            Assert.AreEqual(43, actualB);
        }


        /// <summary>
        /// Write and read in the same transaction.
        /// Read must see the uncommited change.
        /// </summary>
        [Test]
        public void WriteRead()
        {
            var variable = new TV<int>();
            int actual = TransactionManager.Run(() =>
            {
                variable.Value = 42;
                return variable.Value;
            });
            Assert.AreEqual(42, actual);
        }

        /// <summary>
        /// Exceptions must cause a rollback.
        /// </summary>
        [Test]
        public void RollbackOnException()
        {
            var variable = new TV<int>();
            try
            {
                TransactionManager.Run(() =>
                {
                    variable.Value = 42;
                    throw new Exception("exception thrown inside transaction");
                });
            }
            catch (Exception) { }

            int actual = TransactionManager.Run(() => variable.Value);
            Assert.AreEqual(0, actual);
        }

        /// <summary>
        /// Exceptions must be forwarded to the caller unaltered.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidCastException))]
        public void ExceptionPassedOn()
        {
            var variable = new TV<int>();
            TransactionManager.Run(() =>
            {
                variable.Value = 42;
                throw new InvalidCastException("exception thrown inside transaction");
            });
        }

        /// <summary>
        /// Read-only transaction with read operation.
        /// </summary>
        [Test]
        public void ReadOnlyRead()
        {
            var variable = new TV<int>(42);
            int actual = TransactionManager.RunReadOnly(() => variable.Value);
            Assert.AreEqual(42, actual);
        }

        /// <summary>
        /// Write operation in read-only transaction must fail.
        /// </summary>
        [Test]
        [ExpectedException(typeof(ReadOnlyTransactionException))]
        public void ReadOnlyWrite()
        {
            var variable = new TV<int>(0);
            TransactionManager.RunReadOnly(() =>
            {
                variable.Value = 42;
                return 0;
            });
        }
    }
}
