﻿using Medallion.Threading.Internal;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Medallion.Threading.Tests.Data
{
    public abstract class UpgradeableReaderWriterLockConnectionStringStrategyTestCases<TLockProvider, TStrategy, TDb>
        where TLockProvider : TestingUpgradeableReaderWriterLockProvider<TStrategy>, new()
        where TStrategy : TestingConnectionStringSynchronizationStrategy<TDb>, new()
        where TDb : ITestingPrimaryClientDb, new()
    {
        private TLockProvider _lockProvider = default!;

        [SetUp] public void SetUp() => this._lockProvider = new TLockProvider();
        [TearDown] public void TearDown() => this._lockProvider.Dispose();

        /// <summary>
        /// Tests the logic where upgrading a connection stops and restarts the keepalive
        /// </summary>
        [Test]
        public void TestKeepaliveProtectsFromIdleSessionKillerAfterFailedUpgrade()
        {
            var applicationName = DistributedLockHelpers.ToSafeName(
                this._lockProvider.GetUniqueSafeName(),
                maxNameLength: this._lockProvider.Strategy.Db.MaxApplicationNameLength, s => s
            );
            this._lockProvider.Strategy.Db.ConnectionStringBuilder["Application Name"] = applicationName;

            this._lockProvider.Strategy.KeepaliveCadence = TimeSpan.FromSeconds(.1);
            var @lock = this._lockProvider.CreateUpgradeableReaderWriterLock(nameof(TestKeepaliveProtectsFromIdleSessionKillerAfterFailedUpgrade));

            using var idleSessionKiller = new IdleSessionKiller(this._lockProvider.Strategy.Db, applicationName, idleTimeout: TimeSpan.FromSeconds(.25));

            using (@lock.AcquireReadLock())
            {
                var handle = @lock.AcquireUpgradeableReadLock();
                handle.TryUpgradeToWriteLock().ShouldEqual(false);
                handle.TryUpgradeToWriteLockAsync().Result.ShouldEqual(false);
                Thread.Sleep(TimeSpan.FromSeconds(1));
                Assert.DoesNotThrow(() => handle.Dispose());
            }
        }

        /// <summary>
        /// Demonstrates that we don't multi-thread the connection despite running keepalive queries
        /// 
        /// This test is similar to <see cref="ConnectionStringStrategyTestCases{TLockProvider, TStrategy, TDb}.TestKeepaliveDoesNotCreateRaceCondition"/>,
        /// but in this case we additionally test lock upgrading which must pause and restart the keepalive process.
        /// </summary>
        [Test]
        public void TestKeepaliveDoesNotCreateRaceCondition()
        {
            this._lockProvider.Strategy.KeepaliveCadence = TimeSpan.FromMilliseconds(1);

            Assert.DoesNotThrow(() =>
            {
                var @lock = this._lockProvider.CreateUpgradeableReaderWriterLock(nameof(TestKeepaliveDoesNotCreateRaceCondition));
                for (var i = 0; i < 30; ++i)
                {
                    using var handle = @lock.AcquireUpgradeableReadLockAsync().Result;
                    Thread.Sleep(1);
                    handle.UpgradeToWriteLock();
                    Thread.Sleep(1);
                }
            });
        }
    }
}