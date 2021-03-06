﻿using System;
using Sync;
using Xunit;

namespace Coop.Tests.Sync
{
    public class MethodPatcher_Test
    {
        private class A
        {
            [ThreadStatic] public static int? StaticLatestArgument;
            [ThreadStatic] public static int StaticNumberOfCalls;
            public int? LatestArgument;
            public int NumberOfCalls;

            public void SyncedMethod(int iSomeArgument)
            {
                ++NumberOfCalls;
                LatestArgument = iSomeArgument;
            }

            public static void StaticSyncedMethod(int iSomeArgument)
            {
                ++StaticNumberOfCalls;
                StaticLatestArgument = iSomeArgument;
            }
        }

        private static readonly MethodPatch Patch = new MethodPatch(typeof(A))
                                                    .Intercept(nameof(A.SyncedMethod))
                                                    .Intercept(nameof(A.StaticSyncedMethod));

        [Fact]
        private void IsSyncHandlerCalled()
        {
            // Register sync handler
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            int iNumberOfHandlerCalls = 0;

            Assert.True(Patch.TryGetMethod(nameof(A.SyncedMethod), out MethodAccess method));
            method.SetHandler(instance, args => { ++iNumberOfHandlerCalls; });

            // Trigger the handler
            instance.SyncedMethod(42);
            Assert.Equal(0, instance.NumberOfCalls);
            Assert.Equal(1, iNumberOfHandlerCalls);

            method.RemoveHandler(instance);
        }

        [Fact]
        private void OriginalIsCalledIfNoHandlerExists()
        {
            A instance = new A();
            Assert.Equal(0, instance.NumberOfCalls);
            instance.SyncedMethod(42);
            Assert.Equal(1, instance.NumberOfCalls);
        }
    }
}
