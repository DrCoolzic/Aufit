/*!
@file DPLL.cs
<summary>DPLL Class: implements a DPLL similar to the one used in the WD1772 FDC</summary>

 * This Digital Phase Lock Loop uses the algorithm described in US patent 4,780,844\n
 * http://info-coach.fr/atari/documents/general/patents/US4780844.pdf
	
<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tools project.\n\n
The Atari Universal FD Image Tools project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tools project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.\n\n
The Atari Universal FD Image Tools project is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.\n
See the GNU General Public License for more details.\n\n
You should have received a copy of the GNU General Public License
along with the Atari Universal FD Image Tools project; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA\n</div>
	
@author Jean Louis-Guerin
	
*/

namespace WD1772NS {

/// <summary>The PLL Class implements a FD controller DPLL</summary>
public class DPLL {
	/// <summary> 8 MHz clock = 125 ns</summary>
	readonly int PLL_CLK = 125;
	/// <summary>00011101 low_value for phase compensation</summary>
	readonly int LOW_FIX = 29;
	/// <summary>11100011high value for phase compensation</summary>
	readonly int HIGH_FIX = 227;
	/// <summary>bottom value for counter (128 - 9%)</summary>
	readonly int BOTTOM = 116;	
	/// <summary>top value for counter (128 + 9%)</summary>
	readonly int TOP = 140;	

	/// <summary> running time</summary>
	private int currentTime;
	/// <summary>increment counter</summary>
	private bool up;
	/// <summary>decrement counter</summary>
	private bool down;
	/// <summary>select LOW_FIX value</summary>
	private bool low;
	/// <summary>select HIGH_FIX value</summary>
	private bool high;	
	/// <summary>period_count initialized to 128 (128*16=2048=2^11)</summary>
	private int periodCount;
	/// <summary>11 bits adder</summary>
	private int adder;
	/// <summary>lead/lag (msb) history (previous 2 msb)</summary>
	private int history;
	/// <summary> freq correction amount</summary>
	private int freqAmount;	
	/// <summary>phase amount correction</summary>
	private int phaseAmount;
	/// <summary>Floppy density True=HD False=DD</summary>
	private bool density;

	/// <summary>get current PLL clock period in nanoseconds </summary>
	/// <returns>The actual period of the adjusted clock in nanoseconds</returns>
	public int Clock {
		get {
			// clock = (2048 * 125ns) / period_count
			int clock = 256000 / periodCount;
			if (density)
				return clock / 2;
			return clock;
		}
	}


 	/// <summary>The DPLL class Constructor. We reset all internals values</summary>
	/// <param name="den">Set to true for HD MFM FD</param>
	public DPLL(bool den = false) {
		currentTime = 0;			// reset current time
		density = den;				// set density;
		reset();
	}

	private void reset() {
		up = false;					// increment counter false
		down = false;				// decrement counter false
		periodCount = 128;			// period_count initialized to 128 (128*16=2048=2^11)
		adder = 0;					// reset the 11 bits adder
		low = false;				// select LOW_FIX false
		high = false;				// select HIGH_FIX false
		history = 0;				// reset history
		freqAmount = 0;				// reset freq correction amount
		phaseAmount = 0;			// reset phase amount correction
	}


	/// <summary>Find in which inspection window the data pulse is located</summary>
	/// <param name="dataTime">absolute time in nanoseconds of the 
	/// data pulse since the start of the track</param>
	/// <returns>The number of 2µs inspection windows to sample to the next 
	/// data pulse. Normal values are 2,3,4 (corresponding to 4, 6, 8µs 
	/// but any value can be returned.</returns>
	/// <remarks>The DPLL adjust the frequency (duration) and the phase 
	/// (start / stop time) of the inspection window based on the history 
	/// of previously received pulses to correct frequency and/or phase 
	/// variation.
	/// The window size (fixed by periodCount) changed based on MSB error history.
	///	The data position (phase) in window changed based on adder MSB to
	///	try to move data pulse in middle of inspection window
	public int dataInWindow(int dataTime) {
		bool dataFound = false;		// set to true if data found in window
		bool readPulse = false;		// set to true for one loop
		int error = 0;				// MSB + history (3 bits)
		int numWindows = 0;			// number of inspection windows to find data pulse

		// trick => if in High Density we multiply the value by 2 ==> clock / 2
		if (density) dataTime *= 2;

		// here we check for Looong flux like in NFA
		if ((dataTime - currentTime) > 256 * PLL_CLK) {	// 32 µS
			int nw = (dataTime - currentTime) / (16 * PLL_CLK);
			currentTime = dataTime;
			reset();
			return nw;
		}

		// do while the data pulse is not found
		do {
			numWindows++;
			// do while no roll over in the 11 bits adder
			do {
				currentTime += PLL_CLK;
				// up for one loop only
				if (readPulse)
					readPulse = false;

				// check data in inspection window
				if (currentTime >= dataTime) {
					if (!dataFound) // one time
						readPulse = true;
					dataFound = true; // data found
				}

				// adjust period_count based on frequency correction
				if (up && (periodCount < TOP))
					periodCount++;
				if (down && (periodCount > BOTTOM))
					periodCount--;

				// update adder based on phase correction
				if (low)
					adder += LOW_FIX;
				else if (high)
					adder += HIGH_FIX;
				else
					adder += periodCount;

				if (readPulse) {
					// during readPulse we compute frequency and phase correction
					// 3 MSB of adder
					int msb = (adder >> 8) & 0x7;
					// -------------------- //
					// frequency correction //
					// -------------------- //
					// compute error based on current upper MSB bit and history
					switch ((msb & 0x4) + history) {
						case 0: error = 2; break;	// 000 very fast
						case 1: error = 1; break;	// 001 fast
						case 2: error = 0; break;	// 010 OK
						case 3: error = 0; break;	// 011 now adjusted
						case 4: error = 0; break;	// 100 now adjusted
						case 5: error = 0; break;	// 101 OK
						case 6: error = 1; break;	// 110 slow
						case 7: error = 2; break;	// 111 very slow
					}

					// compute and save new history based on adder msb and previous history
					history = ((adder & 0x400) >> 9) | (history >> 1) & 0x7F;

					// set freq_amount based on error history and msb
					// The frequency decoder controls the period of the data inspection window respective 
					// to the error for the 11 bit adder is as follows:
					// error = 0: 
					//				-> no correction necessary <-
					// error = 1:
					//				MSBs input:			7	6	5	4	3	2	1	0
					//				Correction output:  -3  -2  -1   0   0  +1  +2  +3
					// error = 2:
					//				MSBs input:			7	6	5	4	3	2	1	0
					//				Correction output:  -4  -3  -2  -1  +1  +2  +3  +4
					// The most significant bit of the freq_amount controls increment (value 0)
					// or decrement (value 1) of the adder
					switch (error) {
						case 0:
							freqAmount = 0;
							break;

						case 1:
							switch (msb) {
								case 0: freqAmount = 0x3; break;	// 0011
								case 1: freqAmount = 0x2; break;	// 0010
								case 2: freqAmount = 0x1; break;	// 0001
								case 3: freqAmount = 0x0; break;	// 0000
								case 4: freqAmount = 0x8; break;	// 1000
								case 5: freqAmount = 0x9; break;	// 1001
								case 6: freqAmount = 0xA; break;	// 1010
								case 7: freqAmount = 0xB; break;	// 1011
							}
							break;

						case 2:
							switch (msb) {
								case 0: freqAmount = 0x4; break;	// 0100
								case 1: freqAmount = 0x3; break;	// 0011
								case 2: freqAmount = 0x2; break;	// 0010
								case 3: freqAmount = 0x1; break;	// 0001
								case 4: freqAmount = 0x9; break;	// 1001
								case 5: freqAmount = 0xA; break;	// 1010
								case 6: freqAmount = 0xB; break;	// 1011
								case 7: freqAmount = 0xC; break;	// 1100
							}
							break;
					} // set freq amount based on history

					// ---------------- //
					// Phase correction //
					// ---------------- //
					// set phase amount based on adder msbs

					// The phase decoder depends on the value of ADDER_MSBs. If the phase leads, 
					// the most significant bit of phase_amount indicates with a '0', that the next 
					// rollover should appear earlier. In case of a phase lag, the next rollover 
					// should come later (indicated by a '1' of the most significant bit of phase_amount
					// bit 3 used to control high(0) or low(1) select
					switch (msb) {
						case 0: phaseAmount = 0x4; break;	// 0100 huge lead
						case 1: phaseAmount = 0x3; break;	// 0011 large lead
						case 2: phaseAmount = 0x2; break;	// 0010 medium lead
						case 3: phaseAmount = 0x1; break;	// 0001 small lead
						case 4: phaseAmount = 0x9; break;	// 1001 small lag
						case 5: phaseAmount = 0xA; break;	// 1010 medium lag
						case 6: phaseAmount = 0xB; break;	// 1011 large lag
						case 7: phaseAmount = 0xC; break;	// 1100 huge lag
					}

				} // readPulse == true

				else {
					if ((freqAmount & 0x7) != 0)
						freqAmount--;
					if ((phaseAmount & 0x7) != 0)
						phaseAmount--;
				} // readPulse == false

				// set freq correction flags
				if ((freqAmount & 0x07) != 0) {
					// freq correction needed
					if ((freqAmount & 0x8) != 0) {
						up = false;
						down = true;
					}
					else {
						up = true;
						down = false;
					}
				} // freq amount != 0
				else {
					// no freq correction needed
					up = false;
					down = false;
				} // freq amount == 0

				// set phase correction flags
				if ((phaseAmount & 0x7) != 0) {
					// phase correction needed
					if ((phaseAmount & 0x8) != 0) {
						low = true;
						high = false;
					}
					else {
						high = true;
						low = false;
					}
				} // phase correction needed
				else {
					high = false;
					low = false;
				}

			} while (adder < 2048); // do until adder roll over
			adder -= 2048;

		} while (!dataFound); // do until we found data
		return numWindows;
	} // end of dataInWindow

}	// end of class DPLL

}	// name space

