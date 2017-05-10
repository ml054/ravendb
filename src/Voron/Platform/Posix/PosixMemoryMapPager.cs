using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Sparrow;
using Sparrow.Platform.Posix;
using Sparrow.Utils;
using Voron.Global;
using Voron.Impl;

namespace Voron.Platform.Posix
{
    public unsafe class PosixMemoryMapPager : PosixAbstractPager
    {
        private readonly StorageEnvironmentOptions _options;
        private int _fd;
        public readonly long SysPageSize;
        private long _totalAllocationSize;
        private readonly bool _isSyncDirAllowed;
        private readonly bool _copyOnWriteMode;
        public override long TotalAllocationSize => _totalAllocationSize;
        public PosixMemoryMapPager(StorageEnvironmentOptions options, string file, long? initialFileSize = null,
            bool usePageProtection = false) : base(options, usePageProtection)
        {
            _options = options;
            FileName = file;
            _copyOnWriteMode = options.CopyOnWriteMode && file.EndsWith(Constants.DatabaseFilename);
            _isSyncDirAllowed = Syscall.CheckSyncDirectoryAllowed(FileName);

            PosixHelper.EnsurePathExists(FileName);

            _fd = Syscall.open(file, OpenFlags.O_RDWR | OpenFlags.O_CREAT,
                              FilePermissions.S_IWUSR | FilePermissions.S_IRUSR);
            if (_fd == -1)
            {
                var err = Marshal.GetLastWin32Error();
                Syscall.ThrowLastError(err, "when opening " + file);
            }

            SysPageSize = Syscall.sysconf(SysconfName._SC_PAGESIZE);

            _totalAllocationSize = GetFileSize();
            
            if (_totalAllocationSize == 0 && initialFileSize.HasValue)
            {
                _totalAllocationSize = NearestSizeToPageSize(initialFileSize.Value);
            }
            if (_totalAllocationSize == 0 || _totalAllocationSize % SysPageSize != 0 ||
                _totalAllocationSize != GetFileSize())
            {
                _totalAllocationSize = NearestSizeToPageSize(_totalAllocationSize);
                PosixHelper.AllocateFileSpace(_options, _fd, _totalAllocationSize, file);
            }

            if (_isSyncDirAllowed && Syscall.SyncDirectory(file) == -1)
            {
                var err = Marshal.GetLastWin32Error();
                Syscall.ThrowLastError(err, "sync dir for " + file);
            }

            NumberOfAllocatedPages = _totalAllocationSize / Constants.Storage.PageSize;

            SetPagerState(CreatePagerState());
        }


        private long NearestSizeToPageSize(long size)
        {
            if (size == 0)
                return SysPageSize * 16;

            var mod = size % SysPageSize;
            if (mod == 0)
            {
                return size;
            }
            return ((size / SysPageSize) + 1) * SysPageSize;
        }

        private long GetFileSize()
        {            
            FileInfo fi = new FileInfo(FileName);
            return fi.Length;
            
        }

        protected override string GetSourceName()
        {
            return "mmap: " + _fd + " " + FileName;
        }

        protected internal override PagerState AllocateMorePages(long newLength)
        {
            if (Disposed)
                ThrowAlreadyDisposedException();
            var newLengthAfterAdjustment = NearestSizeToPageSize(newLength);

            if (newLengthAfterAdjustment <= _totalAllocationSize)
                return null;

            var allocationSize = newLengthAfterAdjustment - _totalAllocationSize;

            PosixHelper.AllocateFileSpace(_options, _fd, _totalAllocationSize + allocationSize, FileName);

            if (_isSyncDirAllowed && Syscall.SyncDirectory(FileName) == -1)
            {
                var err = Marshal.GetLastWin32Error();
                Syscall.ThrowLastError(err);
            }

            PagerState newPagerState = CreatePagerState();
            if (newPagerState == null)
            {
                var errorMessage = string.Format(
                    "Unable to allocate more pages - unsuccessfully tried to allocate continuous block of virtual memory with size = {0:##,###;;0} bytes",
                    (_totalAllocationSize + allocationSize));

                throw new OutOfMemoryException(errorMessage);
            }

            newPagerState.DebugVerify(newLengthAfterAdjustment);

            SetPagerState(newPagerState);

            _totalAllocationSize += allocationSize;
            NumberOfAllocatedPages = _totalAllocationSize/ Constants.Storage.PageSize;

            return newPagerState;
        }


        private PagerState CreatePagerState()
        {
            var fileSize = GetFileSize();
            var mmflags = _copyOnWriteMode ? MmapFlags.MAP_PRIVATE : MmapFlags.MAP_SHARED;
            var startingBaseAddressPtr = Syscall.mmap64(IntPtr.Zero, (UIntPtr)fileSize,
                                                      MmapProts.PROT_READ | MmapProts.PROT_WRITE,
                                                      mmflags, _fd, 0L);

            if (startingBaseAddressPtr.ToInt64() == -1) //system didn't succeed in mapping the address where we wanted
            {
                var err = Marshal.GetLastWin32Error();

                Syscall.ThrowLastError(err, "mmap on " + FileName);
            }

            NativeMemory.RegisterFileMapping(FileName, startingBaseAddressPtr, fileSize);

            var allocationInfo = new PagerState.AllocationInfo
            {
                BaseAddress = (byte*)startingBaseAddressPtr.ToPointer(),
                Size = fileSize,
                MappedFile = null
            };

            var newPager = new PagerState(this)
            {
                Files = null, // unused
                MapBase = allocationInfo.BaseAddress,
                AllocationInfos = new[] { allocationInfo }
            };

            return newPager;
        }

        public override void Sync(long totalUnsynced)
        {
            //TODO: Is it worth it to change to just one call for msync for the entire file?
            var currentState = GetPagerStateAndAddRefAtomically();
            try
            {
                using (var metric = Options.IoMetrics.MeterIoRate(FileName, IoMetrics.MeterType.DataSync, 0))
                {
                    foreach (var alloc in currentState.AllocationInfos)
                    {
                        metric.IncrementFileSize(alloc.Size);
                        var result = Syscall.msync(new IntPtr(alloc.BaseAddress), (UIntPtr)alloc.Size, MsyncFlags.MS_SYNC);
                        if (result == -1)
                        {
                            var err = Marshal.GetLastWin32Error();
                            Syscall.ThrowLastError(err, "msync on " + FileName);
                        }
                    }
                    metric.IncrementSize(totalUnsynced);
                }
            }
            finally
            {
                currentState.Release();
            }
        }


        public override string ToString()
        {
            return FileName;
        }

        public override void ReleaseAllocationInfo(byte* baseAddress, long size)
        {
            var ptr = new IntPtr(baseAddress);
            var result = Syscall.munmap(ptr, (UIntPtr)size);
            if (result == -1)
            {
                var err = Marshal.GetLastWin32Error();
                Syscall.ThrowLastError(err, "munmap " + FileName);
            }
            NativeMemory.UnregisterFileMapping(FileName, ptr, size);
        }

        internal override void ProtectPageRange(byte* start, ulong size, bool force = false)
        {
            if (size == 0)
                return;

            if (UsePageProtection || force)
            {
                if (Syscall.mprotect(new IntPtr(start), size, ProtFlag.PROT_READ) == 0)
                    return;
                var err = Marshal.GetLastWin32Error();
                Debugger.Break();
            }
        }

        internal override void UnprotectPageRange(byte* start, ulong size, bool force = false)
        {
            if (size == 0)
                return;

            if (UsePageProtection || force)
            {
                if (Syscall.mprotect(new IntPtr(start), size, ProtFlag.PROT_READ | ProtFlag.PROT_WRITE) == 0)
                    return;
                var err = Marshal.GetLastWin32Error();
                Debugger.Break();
            }
        }


        public override void Dispose()
        {
            base.Dispose();
            if (_fd != -1)
            {
                Syscall.close(_fd);
                _fd = -1;
            }
        }
    }
}
