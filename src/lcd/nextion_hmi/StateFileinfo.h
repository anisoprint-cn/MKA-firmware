/*
 * StateFileinfo.h
 *
 *  Created on: 9 ���. 2018 �.
 *      Author: Azarov
 */

#ifndef SRC_LCD_NEXTION_HMI_STATEFILEINFO_H_
#define SRC_LCD_NEXTION_HMI_STATEFILEINFO_H_


#if ENABLED(NEXTION_HMI)

#include "../nextion/library/Nextion.h"
#include "NextionConstants.h"


namespace StateFileinfo {

	void Print_Push(void *ptr);
	void Print_Clean_Continue_Push(void *ptr);
	void Print_Clean_Cancel_Push(void *ptr);
	void Back_Push(void *ptr);

	void Init();
	void Activate(int8_t sd_slot);
	void DrawUpdate();
	void TouchUpdate();

};

#endif


#endif /* SRC_LCD_NEXTION_HMI_STATEFILEINFO_H_ */
