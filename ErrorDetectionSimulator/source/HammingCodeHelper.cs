using System;
using System.Linq;

namespace ErrorDetectionSimulator.source
{
	/// <summary>
	///  A static Hamming Code class, contains helper functions.
	/// </summary>
	public static class HammingCodeHelper
	{
		/// <summary>
		///  Validates a given string containing Hamming Code.
		/// </summary>
		/// <param name="hammingCode">This must be a numeric string.</param>
		/// <returns>A string containing correct Hamming Code or a Valid message.</returns>
		public static string CheckHammingCode(string hammingCode)
		{
			try
			{
				// Return if the characters in the string are not numbers.
				if (!hammingCode.All(Char.IsDigit)) return "\n";

				// Flip the code so we can do the calculations
				hammingCode = ReverseString(hammingCode);

				// This string will output whether the code is valid or has errors and the corrected code.
				string hammingMsg = string.Empty;

				// Get the number of check bits in the code.
				int numberOfCheckBits = GetNumberOfCheckBits(hammingCode);

				// Store the next power of 2.
				int nextPower = 0;

				// Starting check bit
				int currentCheckBit = 0;

				// If we encounter an error in the code
				bool errorDetected = false;

				// The bit that will be in error if we find one.
				int errorBit = 0;

				// Do this for all the check bits.
				while (currentCheckBit < numberOfCheckBits)
				{
					// Get the next check bit 1, 2, 4...
					nextPower = (int)Math.Pow(2, currentCheckBit);

					// Stores check bit values, P1, P2... which will be used the check for even parity.
					string checkString = "";

					// Start at the power of 2, 2 ^ 0 is 1, 1 - 1 = 0 as the array index begins from 0.
					// This will check the parity bit and the data bits protected by it.
					// It increments by 2 * power of 2. So 2 * 2 gives us 1, 2, 5, 6, 9, 10... for a 11-bit Hamming Code
					for (int i = nextPower - 1; i < hammingCode.Length; i += (2 * nextPower))
					{
						// starts from i and loops until the current position, i + current power of 2.
						for (int j = i; j <= ((i + nextPower) - 1) && j < hammingCode.Length; j++)
						{
							// Add bits to the string.
							checkString += hammingCode[j];
						}
					}

					// Now, we have all the bits that are protected by this check bit
					// If its not a even parity then we have found an error
					if (!CheckEvenParity(checkString))
					{
						errorDetected = true;
						errorBit += nextPower;
						hammingMsg += GetNameAt(nextPower) + ", ";
					}

					// Reset the string for next parity bit validation
					checkString = string.Empty;

					// Increment the current parity bit index
					currentCheckBit++;
				}

				// Output the bit that is in error and the correct Hamming Code if we have detected an error.
				// Else, its a valid Hamming Code assuming there is no more than 1-bit error
				if (errorDetected)
				{
					string hammingCodeCorrected = FlipBitAt(hammingCode, errorBit);

					hammingMsg += "in error. Error bit is " + errorBit + "/" + GetNameAt(errorBit) + "\n";
					hammingMsg += "Corrected: " + ReverseString(hammingCodeCorrected) + "\n";
					hammingMsg += "Data: " + ReverseString(RemoveParityBits(hammingCodeCorrected, numberOfCheckBits, true)) + ", removed parity bits\n\n";
                }
				else
				{
					hammingMsg = "Hamming Code is valid.\n\n";
				}

				return hammingMsg;

			}
			catch (Exception)
			{
				// We have hit an iceberg
				return "Unable to validate the Hamming Code.\n\n";
			}
		}

		/// <summary>
		///  Checks for even parity, if number of 0s and 1s in the string are even.
		/// </summary>
		/// <param name="parityBits">A string containing the parity bit and the bits it protects</param>
		/// <returns>True or false</returns>
		public static bool CheckEvenParity(string parityBits)
		{
			int no0s = 0;
			int no1s = 0;

			for (int i = 0; i < parityBits.Length; i++)
			{
				if (parityBits[i] == '0')
				{
					no0s++;
				}
				else
				{
					no1s++;
				}
			}

			if (IsEven(no0s) && IsEven(no1s))
			{
				return true;
			}

			return false;
		}

		public static string RemoveParityBits(string hammingCode, int numberOfCheckBits, bool strReversed)
		{
			string codeData = hammingCode;

			if (!strReversed)
				codeData = ReverseString(codeData);

			int nextPower = 0;

			int bitsRemoved = 0;

			for (int i = 0; i < numberOfCheckBits; i++)
			{
				// Get the next check bit 1, 2, 4...
				nextPower = (int)Math.Pow(2, i);

				codeData = codeData.Remove((nextPower - bitsRemoved - 1), 1);

				bitsRemoved++;
            }


			return codeData;
		}

		public static int GetNumberOfCheckBits(string hammingCode)
		{
			// Store Number of check bits in the code.
			int numberOfCheckBits = 0;

			// Store the next power of 2.
			int nextPower = 0;
			// Calculates number of parity/check bits in the code
			for (int i = 1; nextPower <= hammingCode.Length; i++)
			{
				// Get the next power of 2.
				nextPower = (int)Math.Pow(2, i);
				// Increment number of check bits
				numberOfCheckBits++;
			}

			return numberOfCheckBits;
		}

		/// <summary>
		///  Checks if a given value is divisible by 2 and there is no remainder.
		/// </summary>
		/// <param name="value">A numeric value</param>
		/// <returns>True or false</returns>
		public static bool IsEven(int value)
		{
			return value % 2 == 0;
		}

		/// <summary>
		///  Flips a bit, 0 or 1 in a string at a given position.
		/// </summary>
		/// <param name="s">A string where a bit is to be filliped</param>
		/// <param name="position">The position of the bit</param>
		/// <returns>String with a bit filliped</returns>
		public static string FlipBitAt(string s, int position)
		{
			int bitPos = position - 1;
			char[] data = s.ToCharArray();
			int value = int.Parse(data[bitPos].ToString());
			data[bitPos] = (1 - value).ToString()[0];

			return new string(data);
		}

		/// <summary>
		///  Reverses a given string so "001" becomes "100".
		/// </summary>
		/// <param name="s">String to be reversed</param>
		/// <returns>A reversed string</returns>
		public static string ReverseString(string s)
		{
			char[] arr = s.ToCharArray();
			Array.Reverse(arr);
			return new string(arr);
		}

		/// <summary>
		///  Returns the name at a given bit position, whether its a parity bit or a data bit
		/// </summary>
		/// <param name="bitPosition">Position of the bit</param>
		/// <returns>String, containing name</returns>
		public static string GetNameAt(int bitPosition)
		{
			bool bPowerOfTwo = IsPowerOfTwo((uint)bitPosition);
			int bitNumber = 1;

			for (uint i = 1; i < bitPosition; i++)
			{
				if (bPowerOfTwo)
				{
					if(IsPowerOfTwo(i))
					{
						bitNumber++;
                    }
                }
				else
				{
					if (!IsPowerOfTwo(i))
					{
						bitNumber++;
					}
				}
			}

			if (bPowerOfTwo)
			{
				return "P" + bitNumber;
			}
			else
			{
				return "D" + bitNumber;
			}
		}

		/// <summary>
		///  Checks if a given numeric value is power of two using bitwise operation
		/// </summary>
		/// <param name="x">A unsigned numeric value</param>
		/// <returns>True or false</returns>
		public static bool IsPowerOfTwo(uint x)
		{
			// Does a bitwise operation using & operator
			// If x does not equal 0, which is not a power of 2 and
			// e.g. 4 (100) and 4 - 1 = 3 (011) so (100 & 011) = 000
			return (x != 0) && ((x & (x - 1)) == 0);
		}
	}
}
