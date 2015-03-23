﻿using ExpenseTracker.Repository;
using ExpenseTracker.Repository.Factories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace ExpenseTracker.API.Controllers
{
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Web.Http.Results;
    using System.Web.Http.Routing;

    using ExpenseTracker.API.Helpers;
    using ExpenseTracker.DTO;

    using Marvin.JsonPatch;

    using Newtonsoft.Json;

    public class ExpenseGroupsController : ApiController
    {
        IExpenseTrackerRepository _repository;
        ExpenseGroupFactory _expenseGroupFactory = new ExpenseGroupFactory();

        private const int MaxPageSize = 10;

        public ExpenseGroupsController()
        {
            _repository = new ExpenseTrackerEFRepository(new 
                Repository.Entities.ExpenseTrackerContext());
        }

        public ExpenseGroupsController(IExpenseTrackerRepository repository)
        {
            _repository = repository;
        }    

        [Route("api/expensegroups", Name = "ExpenseGroupList")]
        public IHttpActionResult Get(string sort = "id", string status = null, string userId = null, int page = 1, int pageSize = MaxPageSize)
        {
            try
            {
                int statusId = -1;

                if (status != null)
                {
                    switch (status.ToLower())
                    {
                        case "open":
                            statusId = 1;
                            break;
                        case "confirmed":
                            statusId = 2;
                            break;
                        case "processed":
                            statusId = 3;
                            break;
                    }
                }

                var expenseGroups =
                    _repository.GetExpenseGroups()
                        .ApplySort(sort)
                        .Where(x => (statusId == -1 || x.ExpenseGroupStatusId == statusId))
                        .Where(x => (userId == null || x.UserId == userId))
                        .ToList()
                        .Select(eg => _expenseGroupFactory.CreateExpenseGroup(eg));

                var enumerable = expenseGroups as ExpenseGroup[] ?? expenseGroups.ToArray();
                var totalCount = enumerable.Count();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var urlHelper = new UrlHelper(Request);
                var prevLink = page > 1
                                   ? urlHelper.Link(
                                       "ExpenseGroupList",
                                       new
                                           {
                                               page = page - 1,
                                               pageSize = pageSize,
                                               sort = sort,
                                               status = status,
                                               userid = userId
                                           })
                                   : "";

                var nextLink = page < totalPages ? urlHelper.Link("ExpenseGroupList", 
                    new
                        {
                            page = page + 1,
                            pageSize = pageSize,
                            sort = sort,
                            status = status,
                            userId = userId
                        }) : "";

                var paginationHeader =
                    new
                        {
                            currentPage = page,
                            pageSize = pageSize,
                            totalCount = totalCount,
                            totalPages = totalPages,
                            previousPageLink = prevLink,
                            nextPageLink = nextLink
                        };

                HttpContext.Current.Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));

                return Ok(enumerable
                    .Skip(pageSize * (page -1))
                    .Take(pageSize)
                    .ToList()
                    .Select(x=> _expenseGroupFactory.CreateExpenseGroup(x)));

            }
            catch (Exception)
            {
                return InternalServerError();
            }
        }

        public IHttpActionResult Get(int id)
        {
            try
            {
                var expenseGroup = _repository.GetExpenseGroup(id);

                if (expenseGroup == null)
                {
                    return this.NotFound();
                }

                return Ok(_expenseGroupFactory.CreateExpenseGroup(expenseGroup));
            }
            catch (Exception)
            {

                return this.InternalServerError();
            }
        }

        [HttpPost]
        public IHttpActionResult Post([FromBody] DTO.ExpenseGroup expenseGroup)
        {
            try
            {
                if (expenseGroup == null)
                {
                    return this.BadRequest(Request.Headers.AcceptEncoding.ToString());
                }

                var eg = _expenseGroupFactory.CreateExpenseGroup(expenseGroup);
                var result = _repository.InsertExpenseGroup(eg);

                if (result.Status == RepositoryActionStatus.Created)
                {
                    var newExpenseGroup = _expenseGroupFactory.CreateExpenseGroup(result.Entity);
                    //location of newly created resource
                    return Created(Request.RequestUri + "/" + newExpenseGroup.Id, newExpenseGroup);
                }

                return this.BadRequest();


            }
            catch (Exception)
            {

                return this.InternalServerError();
            }
        }

        public IHttpActionResult Put(int id, [FromBody] DTO.ExpenseGroup expenseGroup)
        {
            try
            {
                if (expenseGroup == null)
                {
                    return BadRequest();
                }

                var eg = _expenseGroupFactory.CreateExpenseGroup(expenseGroup);

                var result = _repository.UpdateExpenseGroup(eg);

                if (result.Status == RepositoryActionStatus.Updated)
                {
                    var updatedExponseGroup = _expenseGroupFactory.CreateExpenseGroup(result.Entity);

                    return this.Ok(updatedExponseGroup);
                }

                if (result.Status == RepositoryActionStatus.NotFound)
                {
                    return this.NotFound();
                }

                return this.BadRequest();
            }
            catch (Exception)
            {

                return this.InternalServerError();
            }
        }

        [HttpPatch]
        public IHttpActionResult Patch(int id, [FromBody] JsonPatchDocument<DTO.ExpenseGroup> expenseGroupPatchDocument)
        {
            try
            {
                if (expenseGroupPatchDocument == null)
                {
                    return this.BadRequest();
                }

                var expenseGroup = _repository.GetExpenseGroup(id);
                if (expenseGroup == null)
                {
                    return this.NotFound();
                }

                var eg = _expenseGroupFactory.CreateExpenseGroup(expenseGroup);

                expenseGroupPatchDocument.ApplyTo(eg);

                var result = _repository.UpdateExpenseGroup(_expenseGroupFactory.CreateExpenseGroup(eg));

                if (result.Status == RepositoryActionStatus.Updated)
                {
                    var patchedExpenseGroup = _expenseGroupFactory.CreateExpenseGroup(result.Entity);
                    return this.Ok(patchedExpenseGroup);
                }

                return this.BadRequest();

            }
            catch (Exception)
            {
                return this.InternalServerError();
            }
        }

        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var result = _repository.DeleteExpenseGroup(id);

                if (result.Status == RepositoryActionStatus.Deleted)
                {
                    return this.StatusCode(HttpStatusCode.NoContent);
                }

                if (result.Status == RepositoryActionStatus.NotFound)
                {
                    return this.NotFound();
                }

                return this.BadRequest();

            }
            catch (Exception)
            {

                return this.InternalServerError();
            }
        }
    }
}
