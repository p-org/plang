import sys

shards = int(sys.argv[1])
rho = float(sys.argv[2])
back_pressure = int(sys.argv[3])

test_content = None
with open("PTst/TestDriver.p", "r") as f:
    test_content = f.readlines()

assert test_content is not None

test_content[17] = test_content[17][:21] + str(shards) + ";\n"
test_content[18] = test_content[18][:26] + str(float(shards)) + ";\n"
test_content[19] = test_content[19][:18] + str(rho) + ";\n"
test_content[20] = test_content[20][:27] + str(bool(back_pressure)).lower() + ";\n"

with open("PTst/TestDriver.p", "w") as f:
    f.writelines(test_content)
