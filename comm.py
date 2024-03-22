Import("env")
import serial

#~ port = env.GetProjectOption("comm_port")

#~ def comm_callback(*args, **kwargs):
    #~ ser = serial.Serial("COM12", rtscts=True, dsrdtr=True)
    #~ while True:
        #~ comm = input()
        #~ print("Sending command: {}".format(comm))
        #~ msg = "{}\r\n".format(comm)
        #~ ser.write(msg.encode())
        #~ ser.flush()
        #~ res = ser.read_until(expected="\r\n")
        #~ print("Received answer: {}".format(res))
        #~ ser.close()


#~ env.AddCustomTarget("comm", None, comm_callback)


def after_upload(source, target, env):
    ser = serial.Serial("COM12", rtscts=True, dsrdtr=True)
    #~ while True:
        #~ comm = input()
    #~ comm = "M1009"
    #~ print("Sending command: {}".format(comm))
    #~ msg = "{}\r\n".format(comm)
    #~ ser.write(msg.encode())
    #~ ser.flush()
    while True:
        res = ser.read_until(expected="\r\n")
        print("COM12: {}".format(res))
        #~ ser.close()

env.AddPostAction("upload", after_upload)
