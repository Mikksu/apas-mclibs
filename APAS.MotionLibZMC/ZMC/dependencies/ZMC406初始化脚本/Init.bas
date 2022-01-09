
main:

	GLOBAL DIM IsEMBPressed
	
	DIM myModule, myId
	
	IsEMBPressed = 0
	
	PRINT CONTROL + SERIAL_NUMBER
	
	
	ECatADInit
	AxisInit


	RUNTASK 10, EMER_TASK

End


'��ʼ��EtherCat����AD�ɼ�ģ��
SUB ECatADInit()

	'����ZMIO300����ADDA��չģ��
	SLOT_SCAN(0)
	?RETURN
	?"node num " NODE_COUNT(0)
	NODE_AIO(0,0)=32
	?"node 0 start aio " NODE_AIO(0,0)
	SLOT_START(0)
	?RETURN


	'�������õ�0V-5V
	SDO_WRITE(0,0,$5001,1,6,6)

ENDSUB


'��ʼ��������
SUB AxisInit()


	' ALM�źŵ���Ч��ƽ
	INVERT_IN(24,ON) 		'Disble the ALM signal
	INVERT_IN(25,ON) 		'Disble the ALM signal
	INVERT_IN(26,ON) 		'Disble the ALM signal
	INVERT_IN(27,ON) 
	INVERT_IN(28,ON) 
	INVERT_IN(29,ON) 

	'����ALM�ź�ʹ�õ�Input�ܽ�
	ALM_IN(0)=24
	ALM_IN(1)=25
	ALM_IN(2)=26
	ALM_IN(3)=27
	ALM_IN(4)=28
	ALM_IN(5)=29

	'���������
	BASE(0,1,2,3,4,5,6)
	ATYPE=7,7,7,0,0,0					'������
	INVERT_STEP=6,6,6,0,0,0			'����ģʽ
	UNITS=1,1,1,1,1,1
	SRAMP=100,100,100,100,100,100
	SPEED=100000,100000,100000,100000,100000,100000
	ACCEL=3000000,3000000,3000000,3000000,3000000,3000000
	DECEL=3000000,3000000,3000000,3000000,3000000,3000000
	FASTDEC=5000000,5000000,5000000,5000000,5000000,5000000


	' �����ŷ�����������������͹滮λ�õķ����Լ���Ƶ��
	'ENCODER_RATIO(-1,1,2) AXIS(0)

ENDSUB


'��ͣ���ؼ������
Sub EMER_TASK()
	While True

		If IN(23)=ON Then
			RAPIDSTOP(2)
			IsEMBPressed=1
			DELAY(5)
		ELSE
			IsEMBPressed=0
		End If
	WEND
End Sub
