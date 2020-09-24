/**
 * MK4duo Firmware for 3D Printer, Laser and CNC
 *
 * Based on Marlin, Sprinter and grbl
 * Copyright (c) 2011 Camiel Gubbels / Erik van der Zalm
 * Copyright (c) 2020 Alberto Cotronei @MagoKimbra
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 */

#include "../../../../MK4duo.h"

FSTRINGVALUE(Com::tStart,"start")
FSTRINGVALUE(Com::tOk,"ok")
FSTRINGVALUE(Com::tOkSpace,"ok ")
FSTRINGVALUE(Com::tError,"Error:")
FSTRINGVALUE(Com::tStatus,"Status:")
FSTRINGVALUE(Com::tWait,"wait")
FSTRINGVALUE(Com::tEcho,"Echo:")
FSTRINGVALUE(Com::tConfig,"Config: ")
FSTRINGVALUE(Com::tCap,"Cap:")
FSTRINGVALUE(Com::tInfo,"Info:")
FSTRINGVALUE(Com::tBusy,"busy:")
FSTRINGVALUE(Com::tResend,"Resend:")
FSTRINGVALUE(Com::tWarning,"Warning:")
FSTRINGVALUE(Com::tNAN,"NAN")
FSTRINGVALUE(Com::tINF,"INF")
FSTRINGVALUE(Com::tPauseCommunication,"// action:pause")
FSTRINGVALUE(Com::tContinueCommunication,"// action:resume")
FSTRINGVALUE(Com::tDisconnectCommunication,"// action:disconnect")

/** Public Parameters */
int8_t Com::serial_port_index = -1;

/** Public Function */
void Com::setBaudrate() {
  uint32_t serial_connect_timeout = millis() + 1000UL;
  MKSERIAL1.begin(BAUDRATE_1);
  #if (SERIAL_PORT_1 != -1)
  	  MKSERIAL1.setInterruptPriority(1);
  #endif
  while (!MKSERIAL1 && PENDING(millis(), serial_connect_timeout)) { /* nada */ }

  #if NUM_SERIAL > 1
    MKSERIAL2.begin(BAUDRATE_2);
	#if (SERIAL_PORT_2 != -1)
		  MKSERIAL2.setInterruptPriority(1);
	#endif
    serial_connect_timeout = millis() + 1000UL;
    while (!MKSERIAL2 && PENDING(millis(), serial_connect_timeout)) { /* nada */ }
  #endif
}

void Com::serialFlush() {
  if (serial_port_index == -1 || serial_port_index == 0) MKSERIAL1.flush();
  #if NUM_SERIAL > 1
    if (serial_port_index == -1 || serial_port_index == 1) MKSERIAL2.flush();
  #endif
}

int Com::serialRead(const uint8_t index) {
  switch (index) {
    case 0: return MKSERIAL1.read();
    #if NUM_SERIAL > 1
      case 1: return MKSERIAL2.read();
    #endif
    default: return -1;
  }
}

bool Com::serialDataAvailable() {
  return false
    || MKSERIAL1.available()
    #if NUM_SERIAL > 1
      || MKSERIAL2.available()
    #endif
  ;
}

bool Com::serialDataAvailable(const uint8_t index) {
  switch (index) {
    case 0: return MKSERIAL1.available();
    #if NUM_SERIAL > 1
      case 1: MKSERIAL2.available();
    #endif
    default: return false;
  }
}

// Functions for serial printing from PROGMEM. (Saves loads of SRAM.)
void Com::printPGM(PGM_P str) {
  while (char c = pgm_read_byte(str++)) {
    if (serial_port_index == -1 || serial_port_index == 0) MKSERIAL1.write(c);
    #if NUM_SERIAL > 1
      if (serial_port_index == -1 || serial_port_index == 1) MKSERIAL2.write(c);
    #endif
  }
}

void Com::print_spaces(uint8_t count) {
  count *= (PROPORTIONAL_FONT_RATIO);
  while (count--) {
    if (serial_port_index == -1 || serial_port_index == 0) MKSERIAL1.write(' ');
    #if NUM_SERIAL > 1
      if (serial_port_index == -1 || serial_port_index == 1) MKSERIAL2.write(' ');
    #endif
  }
}

void Com::print_logic(PGM_P const label, const bool logic) {
  if (label) {
    printPGM(label);
    SERIAL_CHR(':');
  }
  printPGM(logic ? PSTR("true ") : PSTR("false"));
}

void Com::print_onoff(PGM_P const label, const bool on) {
  if (label) {
    printPGM(label);
    SERIAL_CHR(':');
  }
  printPGM(on ? PSTR("on ") : PSTR("off"));
}

void Com::print_yesno(PGM_P const label, const bool yes) {
  if (label) {
    printPGM(label);
    SERIAL_CHR(':');
  }
  printPGM(yes ? PSTR("YES") : PSTR("NO "));
}

void Com::serial_delay(const millis_l ms) {
  const bool was = printer.isSuspendAutoreport();
  printer.setSuspendAutoreport(true);
  HAL::delayMilliseconds(ms);
  printer.setSuspendAutoreport(was);
}
