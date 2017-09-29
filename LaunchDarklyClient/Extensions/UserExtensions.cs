using System;
using System.Collections.Generic;
using Common.Logging;
using Newtonsoft.Json.Linq;

namespace LaunchDarklyClient.Extensions
{
	public static class UserExtensions
	{
		private static readonly ILog log = LogManager.GetLogger(nameof(UserExtensions));

		public static User AndSecondaryKey(this User user, string secondaryKey)
		{
			try
			{
				log.Trace($"Start {nameof(AndSecondaryKey)}");

				user.SecondaryKey = secondaryKey;
				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndSecondaryKey)}");
			}
		}

		public static User AndIpAddress(this User user, string ipAddress)
		{
			try
			{
				log.Trace($"Start {nameof(AndIpAddress)}");

				user.IpAddress = ipAddress;
				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndIpAddress)}");
			}
		}

		public static User AndCountry(this User user, string country)
		{
			try
			{
				log.Trace($"Start {nameof(AndCountry)}");

				if (country.Length != 2)
				{
					throw new ArgumentException("Country should be a 2 character ISO 3166-1 alpha-2 code. e.g. 'US'");
				}

				user.Country = country;
				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndCountry)}");
			}
		}

		public static User AndFirstName(this User user, string firstName)
		{
			try
			{
				log.Trace($"Start {nameof(AndFirstName)}");

				user.FirstName = firstName;
				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndFirstName)}");
			}
		}

		public static User AndLastName(this User user, string lastName)
		{
			try
			{
				log.Trace($"Start {nameof(AndLastName)}");

				user.LastName = lastName;
				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndLastName)}");
			}
		}

		public static User AndName(this User user, string name)
		{
			try
			{
				log.Trace($"Start {nameof(AndName)}");

				user.Name = name;
				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndName)}");
			}
		}

		public static User AndEmail(this User user, string email)
		{
			try
			{
				log.Trace($"Start {nameof(AndEmail)}");

				user.Email = email;
				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndEmail)}");
			}
		}

		public static User AndAnonymous(this User user, bool anonymous)
		{
			try
			{
				log.Trace($"Start {nameof(AndAnonymous)}");

				user.Anonymous = anonymous;
				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndAnonymous)}");
			}
		}

		public static User AndAvatar(this User user, string avatar)
		{
			try
			{
				log.Trace($"Start {nameof(AndAvatar)}");

				user.Avatar = avatar;
				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndAvatar)}");
			}
		}

		public static User AndCustomAttribute(this User user, string attribute, string value)
		{
			try
			{
				log.Trace($"Start {nameof(AndCustomAttribute)}");

				if (attribute == string.Empty)
				{
					throw new ArgumentException("Attribute Name can not be empty");
				}

				user.Custom.Add(attribute, new JValue(value));

				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndCustomAttribute)}");
			}
		}

		public static User AndCustomAttribute(this User user, string attribute, bool value)
		{
			try
			{
				log.Trace($"Start {nameof(AndCustomAttribute)}");

				if (attribute == string.Empty)
				{
					throw new ArgumentException("Attribute Name can not be empty");
				}

				user.Custom.Add(attribute, new JValue(value));

				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndCustomAttribute)}");
			}
		}

		public static User AndCustomAttribute(this User user, string attribute, int value)
		{
			try
			{
				log.Trace($"Start {nameof(AndCustomAttribute)}");

				if (attribute == string.Empty)
				{
					throw new ArgumentException("Attribute Name can not be empty");
				}

				user.Custom.Add(attribute, new JValue(value));

				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndCustomAttribute)}");
			}
		}

		public static User AndCustomAttribute(this User user, string attribute, float value)
		{
			try
			{
				log.Trace($"Start {nameof(AndCustomAttribute)}");

				if (attribute == string.Empty)
				{
					throw new ArgumentException("Attribute Name can not be empty");
				}

				user.Custom.Add(attribute, new JValue(value));

				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndCustomAttribute)}");
			}
		}

		public static User AndCustomAttribute(this User user, string attribute, List<string> value)
		{
			try
			{
				log.Trace($"Start {nameof(AndCustomAttribute)}");

				if (attribute == string.Empty)
				{
					throw new ArgumentException("Attribute Name can not be empty");
				}

				user.Custom.Add(attribute, new JArray(value.ToArray()));

				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndCustomAttribute)}");
			}
		}

		public static User AndCustomAttribute(this User user, string attribute, List<int> value)
		{
			try
			{
				log.Trace($"Start {nameof(AndCustomAttribute)}");

				if (attribute == string.Empty)
				{
					throw new ArgumentException("Attribute Name can not be empty");
				}

				user.Custom.Add(attribute, new JArray(value.ToArray()));

				return user;
			}
			finally
			{
				log.Trace($"End {nameof(AndCustomAttribute)}");
			}
		}
	}
}