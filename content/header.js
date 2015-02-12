window.onscroll = function(e) {
	var header = document.getElementById('header');
	var scrollTarget = document.getElementById('home-header').offsetHeight;
	var content = document.getElementById('page-content');
	if (this.scrollY >= scrollTarget){  
		if(header.className.indexOf('sticky') == -1){
	     	content.className = header.className = header.className + " sticky";
			}
	  }
	  else{
	    content.className = header.className = header.className.replace(' sticky','')
	  }
};