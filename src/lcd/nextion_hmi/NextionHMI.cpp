/*
 * NextionHMI.cpp
 *
 *  Created on: 31 ��� 2018 �.
 *      Author: Azarov
 */

#include "../../../MK4duo.h"

#ifdef NEXTION_HMI

#include "NextionHMI.h"
#include "../nextion/library/Nextion.h"

#include "NextionConstants.h"
#include "StateStatus.h"

namespace {
	bool _nextionOn = false;
	uint8_t _pageID = 0;
}

NexObject NextionHMI::headerText = NexObject(0,  0,  "tH");
NexObject NextionHMI::headerIcon = NexObject(0,  0,  "iH");
NexObject NextionHMI::sdText = NexObject(0,  0,  "tSD");;
NexObject NextionHMI::sdIcon = NexObject(0,  0,  "iSD");;

void NextionHMI::Init() {

	for (uint8_t i = 0; i < 10; i++) {
		ZERO(nexBuffer);
		_nextionOn = nexInit(nexBuffer);
		if (_nextionOn)
		{
			break;
		}
		HAL::delayMilliseconds(1000);
	}

	if (!_nextionOn) {
		SERIAL_MSG("Nextion LCD serial not initialized! \n");
		return;
	}

	//Retreiving model

	  serial_print("\n>>>>>>>>>>>>\n");
	  serial_print(nexBuffer);
	  serial_print("\n>>>>>>>>>>>>\n");

	if (strstr(nexBuffer, "NX4832T035")) {
		SERIAL_MSG("Nextion LCD connected!  \n");
	}
	else
	{
		_nextionOn = false;
		SERIAL_MSG("Nextion LCD NOT connected! \n");
	}

	/*
	//Init Pages
	Status_Init();
	Temperature_Init();
	Maintenance_Init();
	Files_Init();
	Fileinfo_Init();
	Printing_Init();
	Message_Init();
	Movement_Init();



	Status_Activate();*/

	StateStatus::Init();

	StateStatus::Activate();
}

void NextionHMI::DrawUpdate() {
	switch(_pageID) {
	    case PAGE_STATUS : StateStatus::DrawUpdate();
	         break;
	    case PAGE_TEMPERATURE : //Temperature_DrawUpdate();
	         break;
	    case PAGE_FILES :
	         break;
	    case PAGE_FILEINFO : //Fileinfo_DrawUpdate();
	         break;
	    case PAGE_PRINTING : //Printing_DrawUpdate();
	         break;
	    case PAGE_MESSAGE :
	         break;
	    case PAGE_PAUSE :
	         break;
	    case PAGE_CHANGE :
	         break;
	    case PAGE_WIZARD :
	         break;
	    case PAGE_MAINTENANCE : //Maintenance_DrawUpdate();
	         break;
	    case PAGE_MOVEMENT : //Movement_DrawUpdate();
	         break;
	    case PAGE_EXTRUDERS :
	         break;
	    case PAGE_SETTINGS :
	         break;
	    case PAGE_ABOUT :
	         break;
	}
}

void NextionHMI::TouchUpdate() {
}

void NextionHMI::ActivateState(uint8_t state_id) {
	_pageID = state_id;
}

#endif
