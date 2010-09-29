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
using System.Threading;
using TransactionalVariable;

namespace TransactionalVariableTest
{

    /// <summary>
    /// Tests with multiple interacting threads.
    /// </summary>
    [TestFixture]
    public class MultiThreadedTests
    {

        /// <summary>
        /// Checks that transaction execute as if they where serialized.
        /// </summary>
        [Test]
        [Repeat(30)]
        public void Serializability()
        {
            var variableA = new TV<int>();
            var variableB = new TV<int>();

            Thread t1 = new Thread(() =>
            {
                TransactionManager.Run(() =>
                {
                    variableA.Value = 10;
                    Thread.Sleep(20);
                    variableB.Value = 11;
                });
            });

            Thread t2 = new Thread(() =>
            {
                TransactionManager.Run(() =>
                {
                    variableA.Value = 20;
                    Thread.Sleep(20);
                    variableB.Value = 21;
                });
            });

            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            int actual = TransactionManager.Run(() =>
            {
                return variableB.Value - variableA.Value;
            });

            Assert.AreEqual(1, actual);
        }

        /// <summary>
        /// Test with 4 bank accounts and 10 threads transferring 'money' between them.
        /// The sum must always remain 0.
        /// </summary>
        [Test]
        public void BankAccounts()
        {
            var vars = new TV<long>[]{
                new TV<long>(), new TV<long>(), new TV<long>(), new TV<long>()
            };

            Random random = new Random();
            int threadCount = 20;
            Thread[] threads = new Thread[threadCount];
            for (int t = 0; t < threadCount; t++) threads[t] = new Thread(
                (object r) =>
                {
                    Random rand = (Random)r;

                    for (int i = 0; i < 1E3; i++)
                    {
                        int i1 = 0;
                        int i2 = 0;
                        while (i1 == i2)
                        {
                            i1 = rand.Next(4);
                            i2 = rand.Next(4);
                        }
                        int value = rand.Next(10);
                        TransactionManager.Run(() =>
                        {
                            long randVar1Start = vars[i1].Value;
                            long randVar2Start = vars[i2].Value;
                            long randVar1After = randVar1Start + value;
                            long randVar2After = randVar2Start - value;
                            vars[i1].Value = randVar1After;
                            vars[i2].Value = randVar2After;
                            return 0;
                        });
                    }
                }
            );

            foreach (var t in threads) t.Start(new Random(random.Next()));
            foreach (var t in threads) t.Join();

            long sum = TransactionManager.Run(() =>
            {
                return vars.Select(v => v.Value).Sum();
            });

            Assert.AreEqual(0, sum);
        }
    }
}
