﻿using Medallion.Threading.Internal;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Threading.Azure
{
    /// <summary>
    /// Implements <see cref="IDistributedLockHandle"/>
    /// </summary>
    public sealed class AzureBlobLeaseDistributedLockHandle : IDistributedLockHandle
    {
        private AzureBlobLeaseDistributedLock.InternalHandle? _internalHandle;
        private IDisposable? _finalizerRegistration;

        internal AzureBlobLeaseDistributedLockHandle(AzureBlobLeaseDistributedLock.InternalHandle internalHandle)
        {
            this._internalHandle = internalHandle;
            this._finalizerRegistration = ManagedFinalizerQueue.Instance.Register(this, internalHandle);
        }

        /// <summary>
        /// Implements <see cref="IDistributedLockHandle.HandleLostToken"/>
        /// </summary>
        public CancellationToken HandleLostToken => (this._internalHandle ?? throw this.ObjectDisposed()).HandleLostToken;

        /// <summary>
        /// The underlying Azure lease ID
        /// </summary>
        public string LeaseId => (this._internalHandle ?? throw this.ObjectDisposed()).LeaseId;

        /// <summary>
        /// Releases the lock
        /// </summary>
        public void Dispose() => SyncOverAsync.Run(@this => @this.DisposeAsync(), this);

        /// <summary>
        /// Releases the lock asynchronously
        /// </summary>
        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref this._finalizerRegistration, null)?.Dispose();
            return Interlocked.Exchange(ref this._internalHandle, null)?.DisposeAsync() ?? default;
        }
    }
}