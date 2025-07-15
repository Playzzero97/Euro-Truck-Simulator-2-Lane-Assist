# Framework
from ETS2LA.Events import *
from ETS2LA.Plugin import *
import Plugins.Map.data as mapdata

import time
import math
import logging

class Plugin(ETS2LAPlugin):

    description = PluginDescription(
        name="Sway Detection",
        version="1.0.0",
        description="You should enable this",
        modules=["TruckSimAPI"],
        listen=["*.py"],
        tags=["Base"],
        fps_cap=20
    )

    author = Author(
        name="Playzzero97",
        url="https://github.com/Playzzero97",
        icon="https://avatars.githubusercontent.com/u/219891638?v=4"
    )

    def init(self):
        self.last_prompt = 0
        self.cooldown = 10  # seconds

    def run(self):
        data = self.modules.TruckSimAPI.run()
        velocities = data["truckFloat"]["truck_wheelVelocity"]  # array of len 16

        # Front/rear left and right wheels
        left_side = [velocities[i] for i in [0, 2, 4, 6]]
        right_side = [velocities[i] for i in [1, 3, 5, 7]]

        avg_left = sum(left_side) / len(left_side)
        avg_right = sum(right_side) / len(right_side)

        sway_diff = abs(avg_left - avg_right)
        
        # print(sway_diff)

        now = time.time()
        if sway_diff > 0.1 and (now - self.last_prompt) > self.cooldown:
            logging.warning("Truck swaying detected! Consider lowering Steering Smoothness.") # So users can see it back in the console.
            self.notify("Truck swaying detected! Consider lowering Steering Smoothness.")
            self.last_prompt = now
