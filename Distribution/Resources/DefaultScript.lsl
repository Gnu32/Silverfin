integer _DEBUG = true;

// _d - Debug, set _DEBUG to TRUE for debug messages to show up
_d(string m) {
	if (_DEBUG) {
		llOwnerSay("Debug: " + m);
	}
}


default {

    state_entry() {
        _d("Script created");
    } 
	
    touch_start(integer number) { 
        _d("Object touched")
    }
}