from ETS2LA.UI import *

from ETS2LA.Utils.translator import Translate
from ETS2LA.Utils.Game import path as game
from tkinter import filedialog

import logging
import psutil
import os

games = game.FindSCSGames()
game_versions = [game.GetVersionForGame(found_game) for found_game in games]
target_path = "\\bin\\win_x64\\plugins"

data_versions = os.listdir("ETS2LA/Assets/DLLs")
files_for_version = {}
for version in data_versions:
    files_for_version[version] = os.listdir(f"ETS2LA/Assets/DLLs/{version}")
    files_for_version[version].pop(files_for_version[version].index("sources.txt"))

def IsGameRunning():
    execs = ["amtrucks", "eurotrucks2"]
    for p in psutil.process_iter():
        for game in execs:
            try:
                if game in p.name():
                    return True
            except psutil.NoSuchProcess:
                pass # Usually indicates that a process has exited
        
    return False

def GetFilesForVersion(version: str) -> list[str]:
    if version not in files_for_version:
        return []
    return files_for_version[version]

def CheckIfInstalled(path: str, version: str, detailed: bool = False) -> bool | dict:
    files = GetFilesForVersion(version)
    if not os.path.exists(path + target_path):
        if not detailed:
            return False
        
        return_dict = {}
        for file in files:
            return_dict[file] = False
        return return_dict

    return_dict = {}
    for file in files:
        if os.path.exists(path + target_path + "\\" + file):
            return_dict[file] = True
        else:
            if not detailed:
                return False
            
            return_dict[file] = False
    
    if not detailed:
        return True
    
    return return_dict

class Page(ETS2LAPage):
    dynamic = True
    url = "/settings/sdk"
    settings_target = "sdk_installation"
    onboarding_mode = False

    def CanContinue(self):
        for game in games:
            if CheckIfInstalled(game, game_versions[games.index(game)]):
                return True
        
        return False
    
    def OpenSources(self, version: str):
        if os.path.exists("ETS2LA/Assets/DLLs/" + version + "\\sources.txt"):
            os.startfile("ETS2LA/Assets/DLLs/" + version + "\\sources.txt")
        else:
            SendPopup("No sources found.", "error")
    
    def InstallSDK(self, game: str):
        version = game_versions[games.index(game)]
        if version == "Unknown":
            logging.warning(f"Could not find version for {game}, skipping installation")
            return
        
        try:
            if not CheckIfInstalled(game, version):
                logging.info(f"Installing SDKs for {game}")
                os.makedirs(game + target_path, exist_ok=True)
                files = GetFilesForVersion(version)
                for file in files:
                    with open(f"ETS2LA/Assets/DLLs/{version}/{file}", "rb") as f:
                        with open(game + target_path + "\\" + file, "wb") as g:
                            g.write(f.read())
                            
                SendPopup(f"SDKs for {'ETS2 ' if 'Euro Truck Simulator 2' in game else 'ATS '} installed successfully.", "success")
            else:
                logging.info(f"SDKs for {game} already installed, skipping installation")
        except Exception as e:
            SendPopup(f"Please make sure the game is closed and try again. {e.args}", "error")
            logging.error(f"Error installing SDKs for {game}, please make sure the game is closed.")
    
    def UninstallSDK(self, game: str):
        version = game_versions[games.index(game)]
        if version == "Unknown":
            logging.warning(f"Could not find version for {game}, skipping uninstallation")
            return
        
        try:
            if CheckIfInstalled(game, version):
                logging.info(f"Uninstalling SDKs for {game}")
                files = GetFilesForVersion(version)
                for file in files:
                    os.remove(game + target_path + "\\" + file)
                    
                SendPopup(f"SDKs for {'ETS2 ' if 'Euro Truck Simulator 2' in game else 'ATS '} uninstalled successfully.", "success")
            else:
                logging.info(f"SDKs for {game} not installed, skipping uninstallation")
        except Exception as e:
            SendPopup(f"Please make sure the game is closed and try again. {e.args}", "error")
            logging.error(f"Error uninstalling SDKs for {game}, please make sure the game is closed.")
    
    def AddGame(self):
        path = filedialog.askdirectory(title="Select the game directory")
        version = game.GetVersionForGame(path)
        if version == "Unknown":
            SendPopup("Could not find version for the selected game, please select a valid game directory.", "error")
            logging.warning(f"Could not find version for {path}, skipping installation")
            return
        
        games.append(path)
        game_versions.append(version)
        SendPopup(f"Found game version {version}.", "success")
        
    def OpenPath(self, path: str):
        if os.path.exists(path):
            os.startfile(path)
        else:
            SendPopup(f"Path {path} does not exist.", "error")
    
    def render(self):
        if not self.onboarding_mode:
            TitleAndDescription(
                "sdk_install.title",
                "sdk_install.description",
            )
        
        with Container(styles.FlexVertical() + styles.Gap("24px")):
            if games == []:
                red_text = styles.Style()
                red_text.color = "var(--destructive)"
                red_text.font_weight = "bold"
                Text(Translate("sdk_install.no_games"), red_text)
                with Button(action=self.AddGame):
                    Text("Select directory manually")
                        
            else:
                running = IsGameRunning()

                for found_game, version in zip(games, game_versions):
                    not_supported = False
                    
                    file_install_status = []
                    if files_for_version.get(version) is None:
                        not_supported = True
                        is_installed = False
                    else:
                        file_install_status = CheckIfInstalled(found_game, version, detailed=True)
                        if isinstance(file_install_status, bool):
                            not_supported = True
                            file_install_status = []
                            is_installed = False
                        else:    
                            files = GetFilesForVersion(version)
                            is_installed = [file_install_status[file] for file in files] == [True] * len(files)
                        
                    
                    with Container(styles.FlexVertical() + styles.Gap("8px") + styles.Classname("rounded-md border p-4 bg-input/10")):
                        with Container(styles.FlexHorizontal() + styles.Gap("4px")):
                            title = "ETS2 " if "Euro Truck Simulator 2" in found_game else "ATS "
                            title += version
                            Text(title, styles.Classname("font-semibold"))
                            Text("sdk_install.installed" if is_installed else "sdk_install.not_installed", styles.Description())
                        
                        with Container(styles.FlexHorizontal() + styles.Gap("4px")):
                            with Button(action=self.OpenPath, name=found_game, type="link"):
                                Text(found_game, styles.Description() + styles.Classname("text-xs"))
                        
                        if not_supported:
                            red_text = styles.Style()
                            red_text.color = "var(--destructive)"
                            Text("This game version is not supported", red_text)
                        
                        else:   
                            Space(styles.Height("4px"))
                            with Container(styles.FlexHorizontal() + styles.Gap("8px") + styles.Width("100%") + styles.Classname("justify-start w-full")):             
                                icon_style = styles.Style()
                                icon_style.color = "var(--muted-foreground)"
                                icon_style.width = "18px"
                                icon_style.height = "18px"
                                with Tooltip() as t:
                                    with t.trigger as tr:
                                        tr.style = styles.Classname("border rounded-md p-2 bg-input/10 hover:bg-input/30 transition-all")
                                        Icon("files", icon_style)
                                    with t.content:
                                        with Container(styles.FlexVertical() + styles.Gap("4px") + styles.Padding("4px")):
                                            if file_install_status == {}:
                                                Text("No files found.", styles.Description())
                                            else:
                                                text_style = styles.Style()
                                                text_style.color = "var(--foreground)"
                                                for file in file_install_status:
                                                    with Container(styles.FlexHorizontal() + styles.Gap("4px")):
                                                        Text(file, text_style)
                                                        Text("sdk_install.installed" if file_install_status[file] else "sdk_install.not_installed", styles.Description())
                                                
                                                with Button(action=self.OpenSources, name=version, type="link", style=styles.Padding("12px 0px 0px 0px") + styles.Classname("w-max h-max") + styles.Gap("6px")):
                                                    Icon("file")
                                                    Text("File Sources", styles.Classname("text-xs"))
                                    
                                if running:
                                    with Tooltip() as t:
                                        with t.trigger as tr:
                                            tr.style = styles.Classname("w-full")
                                            if is_installed:
                                                with Button(name=found_game, action=self.UninstallSDK, style=styles.Classname("default w-full"), enabled=not running):
                                                    Text("sdk_install.uninstall")
                                            else:
                                                with Button(name=found_game, action=self.InstallSDK, style=styles.Classname("default w-full"), enabled=not running):
                                                    Text("sdk_install.install")
                                        
                                        with t.content:
                                            text_style = styles.Style()
                                            text_style.color = "var(--foreground)"
                                            Text("Please close the game before installing or uninstalling SDKs.", text_style)
                                                
                                else:
                                    if is_installed:
                                        with Button(name=found_game, action=self.UninstallSDK, style=styles.Width("90.5%" if self.onboarding_mode else "93%")):
                                            Text("sdk_install.uninstall")
                                    else:
                                        with Button(name=found_game, action=self.InstallSDK, style=styles.Width("90.5%" if self.onboarding_mode else "93%")):
                                            Text("sdk_install.install")
                                