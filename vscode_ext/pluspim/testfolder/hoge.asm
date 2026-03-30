main:
	sub $t3, $t2, $t1
      sll $t4, $t1, 2
      slt $t5, $zero, $t4
      add $t2, $t0, $t1    # $t2 = 0 + 0xcafe = 0xcafe
      add $t3, $t2, $t1    # $t3 = 0xcafe + 0xcafe = 0x195fc