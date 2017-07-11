/**
 * MK4duo 3D Printer Firmware
 *
 * Based on Marlin, Sprinter and grbl
 * Copyright (C) 2011 Camiel Gubbels / Erik van der Zalm
 * Copyright (C) 2013 - 2017 Alberto Cotronei @MagoKimbra
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

/**
 * planner.h
 *
 * Buffer movement commands and manage the acceleration profile plan
 *
 * Derived from Grbl
 * Copyright (c) 2009-2011 Simen Svale Skogsrud
 */

#ifndef PLANNER_H
#define PLANNER_H

enum BlockFlagBit {
  // Recalculate trapezoids on entry junction. For optimization.
  BLOCK_BIT_RECALCULATE,

  // Nominal speed always reached.
  // i.e., The segment is long enough, so the nominal speed is reachable if accelerating
  // from a safe speed (in consideration of jerking from zero speed).
  BLOCK_BIT_NOMINAL_LENGTH,

  // Start from a halt at the start of this block, respecting the maximum allowed jerk.
  BLOCK_BIT_START_FROM_FULL_HALT,

  // The block is busy
  BLOCK_BIT_BUSY
};

enum BlockFlag {
  BLOCK_FLAG_RECALCULATE          = _BV(BLOCK_BIT_RECALCULATE),
  BLOCK_FLAG_NOMINAL_LENGTH       = _BV(BLOCK_BIT_NOMINAL_LENGTH),
  BLOCK_FLAG_START_FROM_FULL_HALT = _BV(BLOCK_BIT_START_FROM_FULL_HALT),
  BLOCK_FLAG_BUSY                 = _BV(BLOCK_BIT_BUSY)
};

/**
 * struct block_t
 *
 * A single entry in the planner buffer.
 * Tracks linear movement over multiple axes.
 *
 * The "nominal" values are as-specified by gcode, and
 * may never actually be reached due to acceleration limits.
 */
typedef struct {

  uint8_t flag;                             // Block flags (See BlockFlag enum above)

  //unsigned char active_extruder;            // The extruder to move (if E move)
  //unsigned char active_driver;              // Selects the active driver for E

  // Fields used by the bresenham algorithm for tracing the line
  int32_t steps[NUM_AXIS];                  // Step count along each axis
  uint32_t step_event_count;                // The number of step events required to complete this block

  #if ENABLED(COLOR_MIXING_EXTRUDER)
    uint32_t mix_event_count[MIXING_STEPPERS]; // Scaled step_event_count for the mixing steppers
  #endif

  int32_t accelerate_until,                 // The index of the step event on which to stop acceleration
          decelerate_after,                 // The index of the step event on which to start decelerating
          acceleration_rate;                // The acceleration rate used for acceleration calculation

  uint16_t direction_bits;                   // The direction bit set for this block (refers to *_DIRECTION_BIT in config.h)

  // Advance extrusion
  #if ENABLED(LIN_ADVANCE)
    bool use_advance_lead;
    uint32_t abs_adv_steps_multiplier8;     // Factorised by 2^8 to avoid float
  #elif ENABLED(ADVANCE)
    int32_t advance_rate;
    volatile int32_t initial_advance, final_advance;
    float advance;
  #endif

  // Fields used by the motion planner to manage acceleration
  float nominal_speed,                          // The nominal speed for this block in mm/sec
        entry_speed,                            // Entry speed at previous-current junction in mm/sec
        max_entry_speed,                        // Maximum allowable junction entry speed in mm/sec
        millimeters,                            // The total travel of this block in mm
        acceleration;                           // acceleration mm/sec^2

  // Settings for the trapezoid generator
  uint32_t nominal_rate,                        // The nominal step rate for this block in step_events/sec
           initial_rate,                        // The jerk-adjusted step rate at start of block
           final_rate,                          // The minimal rate at exit
           acceleration_steps_per_s2;           // acceleration steps/sec^2

  #if ENABLED(BARICUDA)
    uint32_t valve_pressure, e_to_p_pressure;
  #endif

  uint32_t segment_time;

  #if ENABLED(LASERBEAM)
    uint8_t laser_mode; // CONTINUOUS, PULSED, RASTER
    bool laser_status; // LASER_OFF, LASER_ON
    float laser_ppm; // pulses per millimeter, for pulsed and raster firing modes
    uint32_t laser_duration; // laser firing duration in microseconds, for pulsed and raster firing modes
    uint32_t steps_l; // step count between firings of the laser, for pulsed firing mode
    float laser_intensity; // Laser firing instensity in clock cycles for the PWM timer
    #if ENABLED(LASER_RASTER)
      unsigned char laser_raster_data[LASER_MAX_RASTER_LINE];
    #endif
  #endif 

} block_t;

#define BLOCK_MOD(n) ((n)&(BLOCK_BUFFER_SIZE-1))

class Planner {

  public:

    /**
     * The current position of the tool in absolute steps
     * Recalculated if any axis_steps_per_mm are changed by gcode
     */
    static long position[NUM_AXIS];

    /**
     * A ring buffer of moves described in steps
     */
    static block_t block_buffer[BLOCK_BUFFER_SIZE];
    static volatile uint8_t block_buffer_head,  // Index of the next block to be pushed
                            block_buffer_tail;

    static float  max_feedrate_mm_s[XYZE_N],    // Max speeds in mm per second
                  axis_steps_per_mm[XYZE_N],
                  steps_to_mm[XYZE_N];

    static uint32_t max_acceleration_steps_per_s2[XYZE_N],
                    max_acceleration_mm_per_s2[XYZE_N]; // Use M201 to override by software

    static millis_t min_segment_time;
    static float  min_feedrate_mm_s,
                  min_travel_feedrate_mm_s,
                  acceleration,                     // Normal acceleration mm/s^2  DEFAULT ACCELERATION for all printing moves. M204 SXXXX
                  retract_acceleration[DRIVER_EXTRUDERS],  // Retract acceleration mm/s^2 filament pull-back and push-forward while standing still in the other axes M204 TXXXX
                  travel_acceleration,              // Travel acceleration mm/s^2  DEFAULT ACCELERATION for all NON printing moves. M204 MXXXX
                  max_jerk[XYZE_N];                 // The largest speed change requiring no acceleration

    #if HAS(ABL)
      static bool abl_enabled;              // Flag that bed leveling is enabled
      #if ABL_PLANAR
        static matrix_3x3 bed_level_matrix; // Transform to compensate for bed level
      #endif
    #endif

    #if ENABLED(ENABLE_LEVELING_FADE_HEIGHT)
      static float z_fade_height, inverse_z_fade_height;
    #endif

    #if ENABLED(LIN_ADVANCE)
      static float extruder_advance_k, advance_ed_ratio;
    #endif

  private:

    /**
     * Speed of previous path line segment
     */
    static float previous_speed[NUM_AXIS];

    /**
     * Nominal speed of previous path line segment
     */
    static float previous_nominal_speed;

    /**
     * Limit where 64bit math is necessary for acceleration calculation
 	   */
    static uint32_t cutoff_long;

    #if ENABLED(DISABLE_INACTIVE_EXTRUDER)
      /**
       * Counters to manage disabling inactive extruders
       */
      static uint8_t g_uc_extruder_last_move[DRIVER_EXTRUDERS];
    #endif // DISABLE_INACTIVE_EXTRUDER

    #if ENABLED(XY_FREQUENCY_LIMIT)
      // Used for the frequency limit
      #define MAX_FREQ_TIME long(1000000.0/XY_FREQUENCY_LIMIT)
      // Old direction bits. Used for speed calculations
      static uint16_t old_direction_bits;
      // Segment times (in µs). Used for speed calculations
      static long axis_segment_time[2][3];
    #endif

    #if ENABLED(LIN_ADVANCE)
      static float  position_float[NUM_AXIS];
    #endif

    #if ENABLED(ULTRA_LCD)
      volatile static uint32_t block_buffer_runtime_us; // Theoretical block buffer runtime in µs
    #endif

    /**
     * Last extruder used
     */
    //static uint8_t last_extruder;

  public:

    /**
     * Instance Methods
     */

    Planner();

    void init();

    /**
     * Static (class) Methods
     */

    static void reset_acceleration_rates();
    static void refresh_positioning();

    // Manage fans, paste pressure, etc.
    static void check_axes_activity();

    /**
     * Number of moves currently in the planner
     */
    static uint8_t movesplanned() { return BLOCK_MOD(block_buffer_head - block_buffer_tail + BLOCK_BUFFER_SIZE); }

    static bool is_full() { return (block_buffer_tail == BLOCK_MOD(block_buffer_head + 1)); }

    #if HAS_LEVELING || ENABLED(ZWOBBLE) || ENABLED(HYSTERESIS)
      #define ARG_X float lx
      #define ARG_Y float ly
      #define ARG_Z float lz
    #else
      #define ARG_X const float &lx
      #define ARG_Y const float &ly
      #define ARG_Z const float &lz
    #endif

    #if HAS_LEVELING

      /**
       * Apply leveling to transform a cartesian position
       * as it will be given to the planner and steppers.
       */
      static void apply_leveling(float &lx, float &ly, float &lz);
      static void apply_leveling(float logical[XYZ]) { apply_leveling(logical[X_AXIS], logical[Y_AXIS], logical[Z_AXIS]); }
      static void unapply_leveling(float logical[XYZ]);

    #endif

    /**
     * Planner::_buffer_line
     *
     * Add a new linear movement to the buffer.
     * Doesn't apply the leveling.
     *
     * Leveling and kinematics should be applied ahead of this.
     *
     *  destination   - target position in mm or degrees
     *  fr_mm_s   - (target) speed of the move
     */
    static void _buffer_line(const float destination[XYZE], float fr_mm_s);

    static void _set_position_mm(const float position[XYZE]);

    /**
     * Add a new linear movement to the buffer.
     * The target is NOT translated to delta/scara
     *
     * Leveling will be applied to input on cartesians.
     * Kinematic machines should call buffer_line_kinematic (for leveled moves).
     * (Cartesians may also call buffer_line_kinematic.)
     *
     *  destination - target position in mm or degrees
     *  fr_mm_s     - (target) speed of the move (mm/s)
     */
    static FORCE_INLINE void buffer_line(const float destination[XYZE], const float &fr_mm_s) {
      #if HAS_LEVELING && IS_CARTESIAN
        apply_leveling(destination[X_AXIS], destination[Y_AXIS], destination[Z_AXIS]);
      #endif
      _buffer_line(destination, fr_mm_s);
    }

    /**
     * Add a new linear movement to the buffer.
     * The target is cartesian, it's translated to delta/scara if
     * needed.
     *
     *  ltarget  - x,y,z,e CARTESIAN target in mm
     *  fr_mm_s  - (target) speed of the move (mm/s)
     */
    static FORCE_INLINE void buffer_line_kinematic(const float ltarget[XYZE], const float &fr_mm_s) {
      #if HAS_LEVELING || ENABLED(ZWOBBLE) || ENABLED(HYSTERESIS)
        float lpos[XYZ]={ ltarget[X_AXIS], ltarget[Y_AXIS], ltarget[Z_AXIS] };
        #if HAS_LEVELING
          apply_leveling(lpos);
        #endif
        #if ENABLED(ZWOBBLE)
          // Calculate ZWobble
          zwobble.InsertCorrection(lpos[Z_AXIS]);
        #endif
        #if ENABLED(HYSTERESIS)
          // Calculate Hysteresis
          hysteresis.InsertCorrection(lpos[X_AXIS], lpos[Y_AXIS], lpos[Z_AXIS], ltarget[E_AXIS]);
        #endif
      #else
        const float * const lpos = ltarget;
      #endif

      #if IS_KINEMATIC
        #if MECH(DELTA)
          deltaParams.Transform(lpos);
        #else
          inverse_kinematics(lpos);
        #endif
        _buffer_line(ltarget, fr_mm_s);
      #else
        _buffer_line(ltarget, fr_mm_s);
      #endif
    }

    /**
     * Set the planner.position and individual stepper positions.
     * Used by G92, G28, G29, and other procedures.
     *
     * Multiplies by axis_steps_per_mm[] and does necessary conversion
     * for COREXY / COREXZ / COREYZ to set the corresponding stepper positions.
     *
     * Clears previous speed values.
     */
    static FORCE_INLINE void set_position_mm(const float pos[XYZE]) {
      #if HAS_LEVELING && IS_CARTESIAN
        apply_leveling(destination[X_AXIS], destination[Y_AXIS], destination[Z_AXIS]);
      #endif
      _set_position_mm(pos);
    }
    static void set_position_mm_kinematic(const float position[NUM_AXIS]);
    static void set_position_mm(const AxisEnum axis, const float &v);
    static FORCE_INLINE void set_z_position_mm(const float &z) { set_position_mm(Z_AXIS, z); }
    static FORCE_INLINE void set_e_position_mm(const float &e) { set_position_mm(AxisEnum(E_AXIS), e); }

    /**
     * Sync from the stepper positions. (e.g., after an interrupted move)
     */
    static void sync_from_steppers();

    /**
     * Does the buffer have any blocks queued?
     */
    static bool blocks_queued() { return (block_buffer_head != block_buffer_tail); }

    /**
     * "Discards" the block and "releases" the memory.
     * Called when the current block is no longer needed.
     */
    static void discard_current_block() {
      if (blocks_queued())
        block_buffer_tail = BLOCK_MOD(block_buffer_tail + 1);
    }

    /**
     * The current block. NULL if the buffer is empty.
     * This also marks the block as busy.
     */
    static block_t* get_current_block() {
      if (blocks_queued()) {
        block_t* block = &block_buffer[block_buffer_tail];
        #if ENABLED(ULTRA_LCD)
          block_buffer_runtime_us -= block->segment_time; // We can't be sure how long an active block will take, so don't count it.
        #endif
        SBI(block->flag, BLOCK_BIT_BUSY);
        return block;
      }
      else {
        #if ENABLED(ULTRA_LCD)
          clear_block_buffer_runtime(); // paranoia. Buffer is empty now - so reset accumulated time to zero.
        #endif
        return NULL;
      }
    }

    #if ENABLED(ULTRA_LCD)

      static uint16_t block_buffer_runtime() {
        CRITICAL_SECTION_START
          millis_t bbru = block_buffer_runtime_us;
        CRITICAL_SECTION_END
        // To translate µs to ms a division by 1000 would be required.
        // We introduce 2.4% error her by dividing by 1024.
        // Does not matter because block_buffer_runtime_us is already an, too small, estimation.
        bbru >>= 10;
        // limit to about a minute.
        NOMORE(bbru, 0xFFFFul);
        return bbru;
      }

      static void clear_block_buffer_runtime() {
        CRITICAL_SECTION_START
          block_buffer_runtime_us = 0;
        CRITICAL_SECTION_END
      }

    #endif

    #if ENABLED(AUTOTEMP)
      static float autotemp_max, autotemp_min, autotemp_factor;
      static bool autotemp_enabled;
      static void getHighESpeed();
      static void autotemp_M104_M109();
    #endif

  private:

    /**
     * Get the index of the next / previous block in the ring buffer
     */
    static int8_t next_block_index(int8_t block_index) { return BLOCK_MOD(block_index + 1); }
    static int8_t prev_block_index(int8_t block_index) { return BLOCK_MOD(block_index - 1); }

    /**
     * Calculate the distance (not time) it takes to accelerate
     * from initial_rate to target_rate using the given acceleration:
     */
    static float estimate_acceleration_distance(const float &initial_rate, const float &target_rate, const float &accel) {
      if (accel == 0) return 0; // accel was 0, set acceleration distance to 0
      return (sq(target_rate) - sq(initial_rate)) / (accel * 2.0);
    }

    /**
     * Return the point at which you must start braking (at the rate of -'acceleration') if
     * you start at 'initial_rate', accelerate (until reaching the point), and want to end at
     * 'final_rate' after traveling 'distance'.
     *
     * This is used to compute the intersection point between acceleration and deceleration
     * in cases where the "trapezoid" has no plateau (i.e., never reaches maximum speed)
     */
    static float intersection_distance(const float &initial_rate, const float &final_rate, const float &accel, const float &distance) {
      if (accel == 0) return 0; // accel was 0, set intersection distance to 0
      return (accel * 2 * distance - sq(initial_rate) + sq(final_rate)) / (accel * 4.0);
    }

    /**
     * Calculate the maximum allowable speed at this point, in order
     * to reach 'target_velocity' using 'acceleration' within a given
     * 'distance'.
     */
    static float max_allowable_speed(const float &accel, const float &target_velocity, const float &distance) {
      return SQRT(sq(target_velocity) - 2 * accel * distance);
    }

    static void calculate_trapezoid_for_block(block_t* const block, const float &entry_factor, const float &exit_factor);

    static void reverse_pass_kernel(block_t* const current, const block_t *next);
    static void forward_pass_kernel(const block_t *previous, block_t* const current);

    static void reverse_pass();
    static void forward_pass();

    static void recalculate_trapezoids();

    static void recalculate();

};

#define PLANNER_XY_FEEDRATE() (min(planner.max_feedrate_mm_s[X_AXIS], planner.max_feedrate_mm_s[Y_AXIS]))

extern Planner planner;

#endif // PLANNER_H
