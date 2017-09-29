using System;
using System.Runtime.Serialization;

namespace LaunchDarklyClient.Exceptions
{
	[Serializable]
	public class EvaluationException : Exception
	{
		public EvaluationException()
		{}

		public EvaluationException(string message) : base(message)
		{}

		public EvaluationException(string message, Exception innerException) : base(message, innerException)
		{}

		protected EvaluationException(SerializationInfo info, StreamingContext context) : base(info, context)
		{}
	}
}