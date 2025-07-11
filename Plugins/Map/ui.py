from ETS2LA.Events import *
from ETS2LA.Plugin import *
from ETS2LA.UI import *
from ETS2LA import variables

import ETS2LA.Utils.settings as settings
import Plugins.Map.data as data

class SettingsMenu(ETS2LAPage):
    url = "/settings/map"
    title = "Map"
    location = ETS2LAPageLocation.SETTINGS
    refresh_rate = 0.25

    def get_value_from_data(self, key: str):
        if "data" not in globals():
            return "N/A"
        if key in data.__dict__:
            return data.__dict__[key]
        return "Not Found"

    def handle_navigation(self, *args):
        if args:
            value = args[0]
        else:
            value = not settings.Get("Map", "UseNavigation", True)
        settings.Set("Map", "UseNavigation", value)
        
    def handle_elevation(self, *args):
        if args:
            value = args[0]
        else:
            value = not settings.Get("Map", "SendElevationData", False)
        settings.Set("Map", "SendElevationData", value)
        
    def handle_fps_notices(self, *args):
        if args:
            value = args[0]
        else:
            value = not settings.Get("Map", "DisableFPSNotices", False)
        settings.Set("Map", "DisableFPSNotices", value)
    
    def handle_steering_data(self, *args):
        if args:
            value = args[0]
        else:
            value = not settings.Get("Map", "ComputeSteeringData", True)
        settings.Set("Map", "ComputeSteeringData", value)
        
    def handle_drive_based_on_trailer(self, *args):
        if args:
            value = args[0]
        else:
            value = not settings.Get("Map", "DriveBasedOnTrailer", True)
        settings.Set("Map", "DriveBasedOnTrailer", value)
        
    def handle_steering_smooth_time(self, *args):
        if args:
            value = args[0]
        else:
            value = settings.Get("Map", "SteeringSmoothTime", 0.2)
        settings.Set("Map", "SteeringSmoothTime", value)
    
    def handle_steering_smooth_time(self, value):
        settings.Set("Map", "SteeringSmoothTime", value)
        
    def handle_data_selection(self, value):
        if value:
            settings.Set("Map", "selected_data", value)
        else:
            settings.Set("Map", "selected_data", "")
            
    def handle_data_update(self):
        if self.plugin:
            self.plugin.trigger_data_update()
            
    def handle_internal_visualisation(self, *args):
        if args:
            value = args[0]
        else:
            value = not settings.Get("Map", "InternalVisualisation", False)
        settings.Set("Map", "InternalVisualisation", value)
        
    def handle_override_lane_offsets(self, *args):
        if args:
            value = args[0]
        else:
            value = not settings.Get("Map", "OverrideLaneOffsets", False)
        settings.Set("Map", "OverrideLaneOffsets", value)

    def render(self):
        TitleAndDescription(
            title="map.settings.1.title",
            description="map.settings.1.description",
        )
        
        with Tabs():
            with Tab("map.settings.tab.general.name", container_style=styles.FlexVertical() + styles.Gap("20px")):
                CheckboxWithTitleDescription(
                    title="map.settings.use_navigation.name",
                    description="map.settings.use_navigation.description",
                    default=settings.Get("Map", "UseNavigation", True),
                    changed=self.handle_navigation,
                )
                CheckboxWithTitleDescription(
                    title="Send Elevation",
                    description="When enabled map will send elevation data to the frontend. This data is used to draw the ground in the visualization.",
                    default=settings.Get("Map", "SendElevationData", False),
                    changed=self.handle_elevation,
                )
                CheckboxWithTitleDescription(
                    title="Disable FPS Notices",
                    description="When enabled map will not notify of any FPS related issues.",
                    default=settings.Get("Map", "DisableFPSNotices", False),
                    changed=self.handle_fps_notices,
                )
                
            with Tab("map.settings.tab.steering.name", container_style=styles.FlexVertical() + styles.Gap("20px")):
                CheckboxWithTitleDescription(
                    title="map.settings.compute_steering_data.name",
                    description="map.settings.compute_steering_data.description",
                    default=settings.Get("Map", "ComputeSteeringData", True),
                    changed=self.handle_steering_data,
                )
                CheckboxWithTitleDescription(
                    title="map.settings.drive_based_on_trailer.name",
                    description="map.settings.drive_based_on_trailer.description",
                    default=settings.Get("Map", "DriveBasedOnTrailer", True),
                    changed=self.handle_drive_based_on_trailer,
                )
                SliderWithTitleDescription(
                    title="map.settings.steering_smooth_time.name",
                    description="map.settings.steering_smooth_time.description",
                    default=settings.Get("Map", "SteeringSmoothTime", 0.2),
                    min=0,
                    max=2,
                    step=0.1,
                    changed=self.handle_steering_smooth_time,
                )
                
            with Tab("Data", container_style=styles.FlexVertical() + styles.Gap("20px")):
                import Plugins.Map.utils.data_handler as dh
                index = dh.GetIndex()
                configs = {}
                for key, data in index.items():
                    try:
                        config = dh.GetConfig(data["config"])
                    except: pass
                    if config != {}:
                        configs[key] = config
                        
                with Container(style=styles.FlexVertical() + styles.Gap("4px") + styles.Padding("0px")):
                    Text("NOTE!", styles.Classname("pl-4 font-semibold text-xs"))
                    Text("If you encounter an error after changing the changing the data please restart the plugin! If this doesn't resolve your issue then please contact the data creators or the developers on Discord!", styles.Description() + styles.Classname("pl-4 text-xs"))
        
                ComboboxWithTitleDescription(
                    title="Selected Data",
                    description="Please select the data you want to use. This will begin the download process and Map will be ready once the data is loaded.",
                    default=settings.Get("Map", "selected_data", ""),
                    options=[config["name"] for config in configs.values()],
                    search=ComboboxSearch(
                        placeholder="Search data",
                        empty="No matching data found"
                    ),
                    changed=self.handle_data_selection,
                )
                
                ButtonWithTitleDescription(
                    title="Update Data",
                    description="Update the currently selected data, this can be helpful if the data is corrupted or there has been an update.",
                    text="Update",
                    action=self.handle_data_update
                )
                
                for key, data in index.items():
                    if key not in configs:
                        continue
                    
                    config = configs[key]
                    with Container(style=styles.FlexVertical() + styles.Gap("12px") + styles.Padding("16px") + styles.Classname("border rounded-md bg-input/10")):
                        with Container(style=styles.FlexVertical() + styles.Gap("4px") + styles.Padding("0px")):
                            Text(config["name"], styles.Classname("font-semibold"))
                            Text(config["description"], styles.Description() + styles.Classname("text-xs"))
                            
                        with Container(style=styles.FlexVertical() + styles.Gap("4px") + styles.Padding("0px")):
                            for title, credit in config["credits"].items():
                                with Container(style=styles.FlexHorizontal() + styles.Gap("4px") + styles.Padding("0px")):
                                    Text(title, styles.Description() + styles.Classname("text-xs"))
                                    Text(credit, styles.Classname("text-xs"))

                        with Container(style=styles.FlexHorizontal() + styles.Gap("4px") + styles.Padding("0px")):
                            Text("The", styles.Description() + styles.Classname("text-xs"))
                            Text("download size", styles.Description() + styles.Classname("text-xs"))
                            Text("for this data is", styles.Description() + styles.Classname("text-xs"))
                            Text(f"{config['packed_size'] / 1024 / 1024:.1f} MB", styles.Classname("text-xs"))
                            Text("that will unpack to a", styles.Description() + styles.Classname("text-xs"))
                            Text("total size", styles.Description() + styles.Classname("text-xs"))
                            Text(f"{config['size'] / 1024 / 1024:.1f} MB.", styles.Classname("text-xs"))
             
            if variables.DEVELOPMENT_MODE:
                with Tab("Debug Data", container_style=styles.FlexVertical() + styles.Gap("20px")):
                    with Container(style=styles.FlexHorizontal() + styles.Gap("4px") + styles.Padding("0px")):
                        if self.plugin:
                            with Container(style=styles.FlexVertical() + styles.Gap("4px") + styles.Padding("0px")):
                                Text("Map Data:")
                                Space()
                                Text(f"Current coordinates: ({self.get_value_from_data('truck_x')}, {self.get_value_from_data('truck_z')})", styles.Description() + styles.Classname("text-xs"))
                                Text(f"Current sector: ({self.get_value_from_data('current_sector_x')}, {self.get_value_from_data('current_sector_y')})", styles.Description() + styles.Classname("text-xs"))
                                Text(f"Roads in sector: {len(self.get_value_from_data('current_sector_roads'))}", styles.Description() + styles.Classname("text-xs"))
                                Text(f"Prefabs in sector: {len(self.get_value_from_data('current_sector_prefabs'))}", styles.Description() + styles.Classname("text-xs"))
                                Text(f"Models in sector: {len(self.get_value_from_data('current_sector_models'))}", styles.Description() + styles.Classname("text-xs"))
                            
                            with Container(style=styles.FlexVertical() + styles.Gap("4px") + styles.Padding("0px")):
                                Text("Route Data:")
                                Space()
                                Text(f"Is steering: {self.get_value_from_data('calculate_steering')}", styles.Description() + styles.Classname("text-xs"))
                                Text(f"Route points: {len(self.get_value_from_data('route_points'))}", styles.Description() + styles.Classname("text-xs"))
                                Text(f"Route plan elements: {len(self.get_value_from_data('route_plan'))}", styles.Description() + styles.Classname("text-xs"))
                                Text(f"Routing mode: {settings.Get('Map', 'RoutingMode')}", styles.Description() + styles.Classname("text-xs"))
                                Text(f"Navigation points: {len(self.get_value_from_data('navigation_points'))}", styles.Description() + styles.Classname("text-xs"))
                                Text(f"Has destination: {self.get_value_from_data('dest_company') is not None}", styles.Description() + styles.Classname("text-xs"))
                        
                            with Container(style=styles.FlexVertical() + styles.Gap("4px") + styles.Padding("0px")):
                                Text("Backend Data:")
                                Space()
                                try: Text(f"State: {self.plugin.state.text}, {self.plugin.state.progress:.0f}", styles.Description() + styles.Classname("text-xs"))
                                except: Text("State: N/A", styles.Description() + styles.Classname("text-xs"))
                                try: Text(f"FPS: {1/self.plugin.performance[-1][1]:.0f}", styles.Description() + styles.Classname("text-xs"))
                                except: Text("FPS: Still loading...", styles.Description() + styles.Classname("text-xs"))
                        else:
                            Text("Plugin not loaded, cannot display debug data.", styles.Description() + styles.Classname("text-xs"))
                        
                with Tab("Development", container_style=styles.FlexVertical() + styles.Gap("20px")):
                    if self.plugin:
                        CheckboxWithTitleDescription(
                            title="map.settings.6.name",
                            description="map.settings.6.description",
                            changed=self.handle_internal_visualisation,
                            default=settings.Get("Map", "InternalVisualisation", False),
                        )
                        ButtonWithTitleDescription(
                            title="Reload Lane Offsets",
                            description="Reload the lane offsets from the file. This will take a few seconds.",
                            action=self.plugin.update_road_data
                        )
                        # Add a button to update the offset configuration.
                        ButtonWithTitleDescription(
                            title="Update Per name",
                            description="Update the lane offsets for a specific name.",
                            action=self.plugin.execute_offset_update
                        )
                        CheckboxWithTitleDescription(
                            title="Override Lane Offsets",
                            description="When enabled, existing offsets will be overwritten in the file.",
                            changed=self.handle_override_lane_offsets,
                            default=settings.Get("Map", "OverrideLaneOffsets", False)
                        )
                        ButtonWithTitleDescription(
                            title="Generate Rules",
                            description="Generate rules for the roads based on the lane offsets.",
                            action=self.plugin.generate_rules
                        )
                        ButtonWithTitleDescription(
                            title="Clear Rules",
                            description="Clear the generated rules.",
                            action=self.plugin.clear_rules
                        )
                        ButtonWithTitleDescription(
                            title="Clear Per name",
                            description="Clear the lane offsets for a specific name.",
                            action=self.plugin.clear_lane_offsets
                        )
                        import Plugins.Map.utils.road_helpers as rh
                        per_name = rh.per_name
                        rules = rh.rules
                        
                        with Container(style=styles.FlexVertical() + styles.Gap("8px")):
                            Text("Per Name", styles.Title() + styles.Classname("font-semibold"))
                            for name, rule in per_name.items():
                                with Container(style=styles.FlexHorizontal() + styles.Gap("8px")):
                                    Text(name, styles.Description() + styles.Classname("text-xs"))
                                    Text(f"Offset: {rule}", styles.Description() + styles.Classname("text-xs"))
                            Text("Lane Offsets", styles.Title() + styles.Classname("font-semibold"))
                            for name, rule in rules.items():
                                with Container(style=styles.FlexHorizontal() + styles.Gap("8px")):
                                    Text(name, styles.Description() + styles.Classname("text-xs"))
                                    Text(f"Offset: {rule}", styles.Description() + styles.Classname("text-xs"))
                    
                    else:
                        Text("Plugin not loaded, cannot reload lane offsets.", styles.Description() + styles.Classname("text-xs"))