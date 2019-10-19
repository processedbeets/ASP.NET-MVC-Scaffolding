using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ExampleMVC.Models;

namespace ExampleMVC.Controllers
{   
    public class ExamplesController : Controller
    {
        private ExampleMVCContext context = new ExampleMVCContext();

        //
        // GET: /Examples/

        public ViewResult Index()
        {
            return View(context.Examples.ToList());
        }

        //
        // GET: /Examples/Details/5

        public ViewResult Details(System.Guid id)
        {
            Example example = context.Examples.Single(x => x.ExampleId == id);
            return View(example);
        }

        //
        // GET: /Examples/Create

        public ActionResult Create()
        {
            return View();
        } 

        //
        // POST: /Examples/Create

        [HttpPost]
        public ActionResult Create(Example example)
        {
            if (ModelState.IsValid)
            {
                example.ExampleId = Guid.NewGuid();
                context.Examples.Add(example);
                context.SaveChanges();
                return RedirectToAction("Index");  
            }

            return View(example);
        }
        
        //
        // GET: /Examples/Edit/5
 
        public ActionResult Edit(System.Guid id)
        {
            Example example = context.Examples.Single(x => x.ExampleId == id);
            return View(example);
        }

        //
        // POST: /Examples/Edit/5

        [HttpPost]
        public ActionResult Edit(Example example)
        {
            if (ModelState.IsValid)
            {
                context.Entry(example).State = EntityState.Modified;
                context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(example);
        }

        //
        // GET: /Examples/Delete/5
 
        public ActionResult Delete(System.Guid id)
        {
            Example example = context.Examples.Single(x => x.ExampleId == id);
            return View(example);
        }

        //
        // POST: /Examples/Delete/5

        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(System.Guid id)
        {
            Example example = context.Examples.Single(x => x.ExampleId == id);
            context.Examples.Remove(example);
            context.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}