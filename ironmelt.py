import re
import os
import time
import datetime
import subprocess

path = r"C:\Users\Witold\Documents\Paradox Interactive\Europa Universalis IV\save games\autosave.eu4"
currentTimestamp = os.path.getmtime(path)
latestTimestamp = 0

print("Savegame monitoring started.")

while True:

    currentTimestamp = os.path.getmtime(path)
    timestampString = datetime.datetime.fromtimestamp(currentTimestamp).strftime("%Y-%m-%d %H:%M")
    if currentTimestamp == latestTimestamp:
        print("No newer savegame found.")
        time.sleep(10)
        continue

    found = False
    with open(path,"r") as file:
        print("Analyzing savegame "+timestampString+".")
        latestTimestamp = currentTimestamp
        lines = file.readlines()
        for i in range(len(lines)):
            if i == 0: continue
            match1 = re.search(r"supportive_country",lines[i])
            #match2 = re.search(r"last_looted=.*",lines[i-1])
            #match3 = re.search(r"loot_remaining=.*",lines[i-1])
            #match4 = re.search(r"\}",lines[i-1])
            #match5 = re.search(r"last_razed=.*",lines[i-1])
            if (match1 is not None):
                found = True
                print(timestampString + " "+ str(i))

    if found:
        subprocess.call("taskkill /IM eu4.exe")

    print("Finished.")
