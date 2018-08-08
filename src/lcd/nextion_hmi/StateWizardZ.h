/*
 * StateWizard.h
 *
 *  Created on: 14 ���. 2018 �.
 *      Author: Azarov
 */

#ifndef SRC_LCD_NEXTION_HMI_STATEWIZARDZ_H_
#define SRC_LCD_NEXTION_HMI_STATEWIZARDZ_H_


#if ENABLED(NEXTION_HMI)



#include "../nextion/library/Nextion.h"
#include "NextionConstants.h"


namespace StateWizardZ {

  	void Movement_Push(void *ptr);

  	void Init();
  	void DrawUpdate();
	void ZOffsetS1(void *ptr=0);
	void BuildPlateS1(void *ptr=0);
	void BuildPlateS5(void *ptr=0);

	void TouchUpdate();

};

#endif


#endif /* SRC_LCD_NEXTION_HMI_STATEWIZARDZ_H_ */
