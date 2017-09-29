using System;
using System.Threading.Tasks;

namespace LaunchDarklyClient.Interfaces
{
	internal interface IUpdateProcessor : IDisposable
	{
		TaskCompletionSource<bool> Start();
		bool Initialized();
	}
}